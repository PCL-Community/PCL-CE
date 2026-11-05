using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Log;

/// <summary>
///     游戏实时日志页左栏：维护已打开的游戏监视器列表与各自的日志缓冲区。
///     右栏通过 <see cref="Current" /> 读取当前选中的缓冲区并渲染正文。
/// </summary>
public partial class PageLogLeft : MyPageLeft, IRefreshable
{
    private bool _isBuilding;

    private bool _refreshed;
    private bool _returnScheduled;

    public PageLogLeft()
    {
        InitializeComponent();
        AnimatedControl = PanList;
        Current = this;
        ViewModel.Changed += OnViewModelChanged;
        ViewModel.LogAppended += OnLogAppended;
        ExtraButtonsPanel.GameLogProvider = () => ViewModel.HasLogs;
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) =>
        {
            _refreshed = false;
            ExtraButtonsPanel.Current?.RefreshGameLogButton();
        };
    }

    public static PageLogLeft? Current { get; private set; }

    public PageLogLeftViewModel ViewModel { get; } = new();

    public Dictionary<int, LogBuffer> LogBuffers => ViewModel.LogBuffers;

    public List<KeyValuePair<int, GameWatcher.Watcher>> ShownLogs => ViewModel.ShownLogs;

    public GameWatcher.Watcher? CurrentLog => ViewModel.CurrentLog;

    public int CurrentUuid => ViewModel.CurrentUuid;

    public void Refresh()
    {
        Reload();
    }

    public override void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public override void OnNavigatedFrom()
    {
    }

    protected override void RefreshLocalization()
    {
        Reload();
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        Reload();
        ExtraButtonsPanel.Current?.RefreshGameLogButton();
    }

    private void OnViewModelChanged()
    {
        PageLogRight.Current?.ReloadContent();
        Reload();
        ExtraButtonsPanel.Current?.RefreshGameLogButton();
    }

    private static void OnLogAppended(LogBuffer buffer)
    {
        PageLogRight.Current?.OnLogAppended(buffer);
    }

    /// <summary>一行游戏日志及其级别。</summary>
    public sealed record LogLine(string Text, GameLogLevel Level);

    /// <summary>单个游戏监视器的日志缓冲区。</summary>
    public sealed class LogBuffer
    {
        public ObservableCollection<LogLine> Lines { get; } = [];
    }

    #region 日志接入

    /// <summary>登记一个游戏监视器并切换到它的日志。可在任意线程调用。</summary>
    public void AttachWatcher(GameWatcher.Watcher watcher)
    {
        ViewModel.AttachWatcher(watcher);
    }

    public static ulong GetMaxLogCount()
    {
        return PageLogLeftViewModel.GetMaxLogCount();
    }

    public void SelectionChange(int uuid)
    {
        ViewModel.SelectionChange(uuid);
    }

    public void RemoveItem(int uuid)
    {
        ViewModel.RemoveItem(uuid);
    }

    #endregion

    #region 列表 UI

    private void Reload()
    {
        try
        {
            if (_isBuilding)
                return;
            _isBuilding = true;
            PanList.Children.Clear();

            if (!ViewModel.HasLogs)
            {
                ScheduleReturnIfEmpty();
                return;
            }

            var title = new TextBlock
            {
                Text = PageLogLeftViewModel.InstancesTitle
            };
            title.Classes.Add("SectionTitle");
            PanList.Children.Add(title);

            foreach (var entry in ViewModel.BuildEntries())
            {
                var newItem = new MyListItem
                {
                    Type = MyListItem.CheckType.RadioBox,
                    IsScaleAnimationEnabled = false,
                    MinPaddingRight = 30,
                    Title = entry.Title,
                    Info = entry.Info,
                    Height = 40d,
                    tag = entry.Uuid
                };
                newItem.Changed += (sender, e) =>
                {
                    if (e.raiseByMouse && ((MyListItem)sender).Checked)
                        SelectionChange((int)((MyListItem)sender).tag!);
                };

                var removeButton = new MyIconButton
                {
                    SvgIcon = "lucide/trash-2",
                    LogoScale = 1.1d
                };
                ToolTip.SetTip(removeButton, Lang.Text("Common.Action.Delete"));
                removeButton.Click += (_, _) => RemoveItem(entry.Uuid);
                newItem.Buttons = [removeButton];

                if (entry.Uuid == ViewModel.CurrentUuid)
                    newItem.Checked = true;
                PanList.Children.Add(newItem);
            }

            PageLogLeftViewModel.ShowMaxLinesHintIfNeeded();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "构建游戏实时日志 UI 出错",
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("LogPage.Error.OperationFailed"));
        }
        finally
        {
            _isBuilding = false;
        }
    }

    /// <summary>没有可显示的游戏日志时离开日志页，与 WPF 保持一致的返回行为。</summary>
    private void ScheduleReturnIfEmpty()
    {
        if (_returnScheduled)
            return;
        _returnScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _returnScheduled = false;
            if (ViewModel.HasLogs)
                return;
            if (PageRouter.Current.Page != PageType.GameLog)
                return;
            PageRouter.Back();
        });
    }

    #endregion
}