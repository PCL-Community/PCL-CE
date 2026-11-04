using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Navigation;
using PCL.Network;
using PCL.UI.Controls;
using PCL.UI.Seams;

namespace PCL.Views.Shell;

/// <summary>
///     主窗口右下角的快捷按钮组：更新重启、返回顶部、任务管理、愚人节、关闭游戏、游戏日志、背景音乐。
///     每个按钮通过 <see cref="MyExtraButton.showCheck" /> 按实时状态显隐，状态在页面切换与定时轮询时刷新。
/// </summary>
public sealed class ExtraButtonsPanel : StackPanel, ISeamShellActions
{
    private static readonly TimeSpan StateRefreshInterval = TimeSpan.FromSeconds(1d);

    private readonly MyExtraButton _april;
    private readonly MyExtraButton _backToTop;
    private readonly MyExtraButton _download;
    private readonly MyExtraButton _log;
    private readonly MyExtraButton _music;
    private readonly MyExtraButton _shutdown;
    private readonly DispatcherTimer _stateTimer;

    private readonly MyExtraButton _updateRestart;
    private ScrollViewer? _hookedScroll;

    private string? _updateRestartTooltip;

    public ExtraButtonsPanel()
    {
        Current = this;
        SeamHosts.Shell = this;
        Orientation = Orientation.Vertical;
        Spacing = 0d;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(15d);

        _updateRestart = CreateButton(
            "lucide/refresh-cw",
            "Main.Extra.UpdateRestart.ToolTip",
            UpdateRestart);
        _backToTop = CreateButton(
            "lucide/arrow-up-to-line",
            "Main.Extra.BackToTop.ToolTip",
            BackToTop);
        _download = CreateButton(
            "lucide/download",
            "Main.Extra.TaskManager.ToolTip",
            ShowTaskManager);
        _april = CreateButton("lucide/flag", "Main.Extra.GiveUp.ToolTip", AprilGiveUp);
        _shutdown = CreateButton(
            "lucide/power",
            "Main.Extra.ShutdownMinecraft.ToolTip",
            ShutdownMinecraft);
        _log = CreateButton("lucide/scroll-text", "Main.Extra.GameLog.ToolTip", ShowGameLog);
        _music = CreateButton(string.Empty, string.Empty, ToggleMusic);
        _music.CanRightClick = true;
        _music.RightClick += (_, _) => MusicNextAction?.Invoke();

        _updateRestart.showCheck = () => UpdateManager.isUpdateWaitingRestart;
        _backToTop.showCheck = () => CanScrollToTop();
        _download.showCheck = () => NetFacade.HasDownloadingTask()
            && PageRouter.Current.Page != PageType.TaskManager;
        _april.showCheck = () => Basics.IsAprilFool
            && !IsAprilGiveup
            && PageRouter.Current.Page == PageType.Launch;
        _shutdown.showCheck = () => GameWatcher.hasRunningMinecraft;
        _log.showCheck = () => ShowLogButton();
        _music.showCheck = () => SeamCapabilities.MediaAvailable
            && MusicStateProvider?.Invoke() is { } state
            && state.Visible;

        _stateTimer = new DispatcherTimer
        {
            Interval = StateRefreshInterval
        };
        _stateTimer.Tick += (_, _) => RefreshAllStates();
        Loaded += (_, _) =>
        {
            RefreshAllStates();
            _stateTimer.Start();
        };
    }

    public static ExtraButtonsPanel? Current { get; private set; }

    public static bool IsAprilGiveup { get; private set; }

    public static Func<MusicButtonState?>? MusicStateProvider { get; set; }

    public static Action? MusicToggleAction { get; set; }

    public static Action? MusicNextAction { get; set; }

    public static Func<bool>? GameLogProvider { get; set; }

    public static Action? AprilGiveupResetAction { get; set; }

    private static MyScrollViewer? CurrentScroll =>
        (PageRouter.CurrentPage as MyPageRight)?.PanScroll;

    public void RefreshDownloadButton()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshDownloadButton);
            return;
        }

        _download.ShowRefresh();
        _download.Progress = Math.Clamp(Loading.loaderTaskbarProgress, 0d, 1d);
    }

    public void ShowUpdateRestartButton(string tooltip)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowUpdateRestartButton(tooltip));
            return;
        }

        _updateRestartTooltip = tooltip;
        _updateRestart.Show = true;
        ApplyUpdateRestartTooltip();
    }

    public void RefreshShutdownButton()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshShutdownButton);
            return;
        }

        _shutdown.ShowRefresh();
    }

    public void RefreshAllStates()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshAllStates);
            return;
        }

        try
        {
            UpdateScrollHook();
            _updateRestart.ShowRefresh();
            RefreshUpdateRestartTooltip();
            _backToTop.ShowRefresh();
            _download.ShowRefresh();
            _april.ShowRefresh();
            _shutdown.ShowRefresh();
            _log.ShowRefresh();
            _music.ShowRefresh();
            RefreshMusicAppearance();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"刷新快捷按钮失败：{ex.Message}");
        }
    }

    public void UpdateButtonProgress(double progress)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateButtonProgress(progress));
            return;
        }

        _download.Progress = Math.Clamp(progress, 0d, 1d);
    }

    public void RefreshBackButton()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshBackButton);
            return;
        }

        _backToTop.ShowRefresh();
    }

    private MyExtraButton CreateButton(string icon, string tipKey, Action action)
    {
        var button = new MyExtraButton
        {
            SvgIcon = icon,
            Show = false
        };
        if (!string.IsNullOrEmpty(tipKey)) ToolTip.SetTip(button, Lang.Text(tipKey));
        button.Click += (_, _) => action();
        Children.Add(button);
        return button;
    }

    private void RefreshUpdateRestartTooltip()
    {
        if (_updateRestartTooltip is not null)
        {
            ApplyUpdateRestartTooltip();
            return;
        }

        var tip = Lang.Text("Main.Extra.UpdateRestart.ToolTip");
        if (!string.Equals(ToolTip.GetTip(_updateRestart) as string, tip, StringComparison.Ordinal))
            ToolTip.SetTip(_updateRestart, tip);
    }

    private void ApplyUpdateRestartTooltip()
    {
        if (_updateRestartTooltip is not null)
            ToolTip.SetTip(_updateRestart, _updateRestartTooltip);
    }

    private void RefreshMusicAppearance()
    {
        var state = MusicStateProvider?.Invoke();
        if (state is null) return;

        _music.SvgIcon = state.Value.Playing ? "lucide/music" : "lucide/play";
        _music.LogoScale = state.Value.Playing ? 1d : 0.8d;
        if (!string.IsNullOrEmpty(state.Value.TipText))
            ToolTip.SetTip(_music, state.Value.TipText);
    }

    private void UpdateScrollHook()
    {
        var scroll = CurrentScroll;
        if (ReferenceEquals(scroll, _hookedScroll)) return;
        if (_hookedScroll is not null) _hookedScroll.ScrollChanged -= OnScrollChanged;
        _hookedScroll = scroll;
        if (_hookedScroll is not null) _hookedScroll.ScrollChanged += OnScrollChanged;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        _backToTop.ShowRefresh();
    }

    private static bool ShowLogButton()
    {
        if (SeamHosts.GameLog is null) return false;
        if (PageRouter.Current.Page == PageType.GameLog) return false;
        return GameLogProvider?.Invoke() ?? GameWatcher.mcWatcherList.Count > 0;
    }

    private static bool CanScrollToTop()
    {
        var scroll = CurrentScroll;
        if (scroll is not { IsVisible: true }) return false;
        var windowHeight = MainWindow.Current?.Bounds.Height ?? 500d;
        return scroll.Offset.Y > windowHeight + (Current?._backToTop.Show == true ? 0d : 700d);
    }

    private void BackToTop()
    {
        var scroll = CurrentScroll;
        if (scroll is null)
        {
            LogWrapper.Warn("UI", "无法返回顶部：当前页面没有可滚动区域");
            return;
        }

        scroll.PerformVerticalOffsetDelta(-scroll.Offset.Y);
    }

    private static void ShowTaskManager()
    {
        PageRouter.Navigate(PageType.TaskManager);
    }

    private static void ShowGameLog()
    {
        PageRouter.Navigate(PageType.GameLog);
    }

    private static void UpdateRestart()
    {
        UpdateManager.UpdateRestart(true);
    }

    private static void AprilGiveUp()
    {
        if (!Basics.IsAprilFool || IsAprilGiveup) return;
        IsAprilGiveup = true;
        UiSeamInteraction.Hint("=D", NotificationType.Success);
        try
        {
            AprilGiveupResetAction?.Invoke();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("UI", $"愚人节状态复位失败：{ex.Message}");
        }

        Current?.RefreshAllStates();
    }

    private static void ShutdownMinecraft()
    {
        try
        {
            GameLaunch.mcLaunchLoaderReal?.Abort();
            foreach (var watcher in GameWatcher.mcWatcherList.ToList()) watcher.Kill();
            UiSeamInteraction.Hint(
                Lang.Text("Main.ShutdownMinecraft.Success"),
                NotificationType.Success);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "UI", "强制关闭所有 Minecraft 失败");
        }
    }

    private static void ToggleMusic()
    {
        MusicToggleAction?.Invoke();
    }

    /// <summary>音乐按钮的展示状态。</summary>
    public readonly record struct MusicButtonState(
        bool Visible,
        bool Playing,
        string? TipText);
}