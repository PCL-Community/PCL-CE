using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.UI.Game.Log;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.Views.Shell;

namespace PCL.Views.Pages.Log;

/// <summary>游戏实时日志页左栏：维护已打开的游戏会话与各自的日志缓冲区。右栏通过 <see cref="Current" /> 读取当前缓冲区并渲染正文。</summary>
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

    public GameLogListViewModel ViewModel { get; } = new();

    public Dictionary<int, GameLogBuffer> LogBuffers => ViewModel.Buffers;

    public List<KeyValuePair<int, GameSession>> ShownSessions => ViewModel.ShownSessions;

    public GameSession? CurrentSession => ViewModel.CurrentSession;

    public int CurrentUuid => ViewModel.CurrentUuid;

    public void Refresh()
    {
        Reload();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        RefreshOnNavigate();
    }

    public override void OnNavigatedFrom()
    {
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

    private static void OnLogAppended(GameLogBuffer buffer)
    {
        PageLogRight.Current?.OnLogAppended(buffer);
    }

    public void AttachSession(GameSession session)
    {
        ViewModel.AttachSession(session);
    }

    public static ulong GetMaxLogCount()
    {
        return GameLogListViewModel.GetMaxLogCount();
    }

    public void SelectionChange(int uuid)
    {
        ViewModel.SelectionChange(uuid);
    }

    public void RemoveItem(int uuid)
    {
        ViewModel.RemoveItem(uuid);
    }

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

            var title = new TextBlock();
            title.Bind(
                TextBlock.TextProperty,
                new Binding(nameof(GameLogListViewModel.InstancesTitle))
                {
                    Source = ViewModel
                });
            title.Classes.Add("SectionTitle");
            PanList.Children.Add(title);

            foreach (var entry in ViewModel.BuildEntries())
            {
                var newItem = new MyListItem
                {
                    Type = MyListItem.CheckType.RadioBox,
                    IsScaleAnimationEnabled = false,
                    MinPaddingRight = 30,
                    Height = 40d,
                    Tag = entry.Uuid
                };
                newItem.Bind(
                    MyListItem.TitleProperty,
                    new Binding(nameof(GameLogSessionEntry.Title))
                    {
                        Source = entry
                    });
                newItem.Bind(
                    MyListItem.InfoProperty,
                    new Binding(nameof(GameLogSessionEntry.Info))
                    {
                        Source = entry
                    });
                newItem.Changed += (sender, e) =>
                {
                    if (e.raiseByMouse && ((MyListItem)sender).Checked)
                        SelectionChange((int)((MyListItem)sender).Tag!);
                };

                var removeButton = new MyIconButton
                {
                    SvgIcon = "lucide/trash-2",
                    LogoScale = 1.1d
                };
                removeButton.SetLocalizedValue(ToolTip.TipProperty, "Common.Action.Delete");
                removeButton.Click += (_, _) => RemoveItem(entry.Uuid);
                newItem.Buttons = [removeButton];

                if (entry.Uuid == ViewModel.CurrentUuid)
                    newItem.Checked = true;
                PanList.Children.Add(newItem);
            }

            GameLogListViewModel.ShowMaxLinesHintIfNeeded();
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
            if (PageRouter.Current.Page != PageIds.GameLog)
                return;
            PageRouter.Back();
        });
    }
}