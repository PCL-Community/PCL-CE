using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
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
    private const int MaxLinesDefault = 500;
    private bool _isBuilding;

    private bool _refreshed;
    private bool _returnScheduled;

    public PageLogLeft()
    {
        InitializeComponent();
        AnimatedControl = PanList;
        Current = this;
        ExtraButtonsPanel.GameLogProvider = () => Current is { ShownLogs.Count: > 0 };
        AttachedToVisualTree += (_, _) => RefreshOnNavigate();
        DetachedFromVisualTree += (_, _) =>
        {
            _refreshed = false;
            ExtraButtonsPanel.Current?.RefreshGameLogButton();
        };
    }

    public static PageLogLeft? Current { get; private set; }

    public Dictionary<int, LogBuffer> LogBuffers { get; } = [];

    public List<KeyValuePair<int, GameWatcher.Watcher>> ShownLogs { get; } = [];

    public GameWatcher.Watcher? CurrentLog { get; private set; }

    public int CurrentUuid { get; private set; } = -1;

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
        if (watcher is null)
            return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => AttachWatcher(watcher));
            return;
        }

        var uuid = LauncherCommon.GetUuid();
        ShownLogs.Add(new KeyValuePair<int, GameWatcher.Watcher>(uuid, watcher));
        watcher.LogOutput += OnLogOutput;
        LogBuffers[uuid] = new LogBuffer();
        ExtraButtonsPanel.Current?.RefreshGameLogButton();
        SelectionChange(uuid);
    }

    private void OnLogOutput(GameWatcher.Watcher sender, GameWatcher.LogOutputEventArgs e)
    {
        var uuid = -1;
        foreach (var item in ShownLogs.Where(item => ReferenceEquals(item.Value, sender)))
        {
            uuid = item.Key;
            break;
        }

        if (uuid < 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (!LogBuffers.TryGetValue(uuid, out var buffer))
                return;
            buffer.Lines.Add(new LogLine(e.logText, e.Level));
            TrimBuffer(buffer);
            if (uuid == CurrentUuid)
                PageLogRight.Current?.OnLogAppended(buffer);
        });
    }

    private static void TrimBuffer(LogBuffer buffer)
    {
        var max = GetMaxLogCount();
        while ((ulong)buffer.Lines.Count > max)
            buffer.Lines.RemoveAt(0);
    }

    public static ulong GetMaxLogCount()
    {
        var maxLog = (ulong)Config.System.MaxGameLog;
        return maxLog switch
        {
            <= 5UL => (ulong)Math.Round(maxLog * 10m + 50m),
            <= 13UL => (ulong)Math.Round(maxLog * 50m - 150m),
            <= 28UL => (ulong)Math.Round(maxLog * 100m - 800m),
            _ => ulong.MaxValue
        };
    }

    public void SelectionChange(int uuid)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SelectionChange(uuid));
            return;
        }

        if (uuid <= 0)
        {
            CurrentUuid = -1;
            CurrentLog = null;
        }
        else
        {
            foreach (var item in ShownLogs.Where(item => item.Key == uuid))
            {
                CurrentUuid = uuid;
                CurrentLog = item.Value;
                break;
            }
        }

        PageLogRight.Current?.ReloadContent();
        Reload();
    }

    public void RemoveItem(int uuid)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => RemoveItem(uuid));
            return;
        }

        for (var i = ShownLogs.Count - 1; i >= 0; i--)
        {
            var item = ShownLogs[i];
            if (item.Key != uuid)
                continue;

            item.Value.LogOutput -= OnLogOutput;
            ShownLogs.RemoveAt(i);
            LogBuffers.Remove(uuid);

            if (CurrentUuid == uuid)
            {
                if (ShownLogs.Count == 0)
                {
                    SelectionChange(-1);
                }
                else
                {
                    var nextIndex = Math.Min(i, ShownLogs.Count - 1);
                    SelectionChange(ShownLogs[nextIndex].Key);
                }
            }
            else
            {
                PageLogRight.Current?.ReloadContent();
                Reload();
            }

            ExtraButtonsPanel.Current?.RefreshGameLogButton();
            break;
        }
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

            if (ShownLogs.Count == 0)
            {
                ScheduleReturnIfEmpty();
                return;
            }

            var title = new TextBlock
            {
                Text = Lang.Text("LogPage.Left.InstancesTitle")
            };
            title.Classes.Add("SectionTitle");
            PanList.Children.Add(title);

            foreach (var (uuid, watcher) in ShownLogs)
            {
                var newItem = new MyListItem
                {
                    Type = MyListItem.CheckType.RadioBox,
                    IsScaleAnimationEnabled = false,
                    MinPaddingRight = 30,
                    Title = SafeGetVersionName(watcher),
                    Info = GetItemInfo(watcher),
                    Height = 40d,
                    tag = uuid
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
                removeButton.Click += (_, _) => RemoveItem(uuid);
                newItem.Buttons = [removeButton];

                if (uuid == CurrentUuid)
                    newItem.Checked = true;
                PanList.Children.Add(newItem);
            }

            if (!States.Hint.MaxGameLog)
            {
                States.Hint.MaxGameLog = true;
                HintService.Show(Hints.Info(Lang.Text("LogPage.MaxLines.Hint", MaxLinesDefault)));
            }
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
            if (ShownLogs.Count > 0)
                return;
            if (PageRouter.Current.Page != PageType.GameLog)
                return;
            PageRouter.Back();
        });
    }

    private static string SafeGetVersionName(GameWatcher.Watcher watcher)
    {
        try
        {
            return watcher.version?.Name ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string GetItemInfo(GameWatcher.Watcher watcher)
    {
        try
        {
            var info = watcher.version?.Info?.ToString() ?? string.Empty;
            var startTime = watcher.gameProcess?.StartTime ?? DateTime.MinValue;
            return $"{info} - {Lang.Date(startTime, "T")}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    #endregion
}