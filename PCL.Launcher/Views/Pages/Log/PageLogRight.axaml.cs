using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.UI.Game.Log;
using PCL.Core.UI.Theme;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Log;

/// <summary>游戏实时日志页右栏：按级别着色渲染当前日志，并提供导出、清空、结束进程与线程快照操作。 正文使用可选中文本与虚拟化列表，避免长日志一次性创建全部控件。</summary>
public partial class PageLogRight : MyPageRight, IRefreshable
{
    private bool _refreshed;

    private GameSession? _subscribedSession;

    public PageLogRight()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Current = this;
        DataContext = ViewModel;
        PanLog.ItemTemplate =
            new FuncDataTemplate<GameLogEntry>((line, _) => CreateLogLine(line));

        BtnOperationExport.Click += (_, _) => _ = ViewModel.ExportLogAsync();
        BtnOperationClear.Click += (_, _) =>
        {
            if (ViewModel.ClearCurrentBuffer())
                Dispatcher.UIThread.Post(() => PanBack.ScrollToHome());
        };
        BtnOperationKill.Click += (_, _) => ViewModel.TerminateCurrentSession();
        BtnOperationExportStackDump.Click += (_, _) => _ = ViewModel.ExportStackDumpAsync();
        SliderMaxLog.Change += (_, _) => ViewModel.MaxLogLines = SliderMaxLog.Value;
        SliderMaxLog.getHintText = GameLogContentViewModel.FormatMaxLogHint;
        ViewModel.PropertyChanged += (_, args) =>
        {
            if (string.IsNullOrEmpty(args.PropertyName))
                RefreshCounters();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _refreshed = false;
            UnsubscribeCurrentSession();
        };
    }

    public static PageLogRight? Current { get; private set; }

    public GameLogContentViewModel ViewModel { get; } = new();

    private static PageLogLeft? Left => PageLogLeft.Current;

    public void Refresh()
    {
        ReloadContent();
    }

    public override void OnNavigatedTo(PageParameter? parameter)
    {
        if (_refreshed)
        {
            ReloadContent();
            return;
        }

        _refreshed = true;
        ReloadContent();
    }

    public override void OnNavigatedFrom()
    {
        UnsubscribeCurrentSession();
    }

    public void ReloadContent()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ReloadContent);
            return;
        }

        ViewModel.Bind(Left?.ViewModel);
        RefreshCounters();
        if (ViewModel.HasLog && Left?.CurrentSession is { } session)
            SubscribeCurrentSession(session);
        else
            UnsubscribeCurrentSession();
        ScrollToEndIfNeeded();
        LogVirtualizationState();
    }

    private void LogVirtualizationState()
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                if (PanLog.ItemsPanelRoot is VirtualizingStackPanel panel)
                    LogWrapper.Debug(
                        "UI",
                        $"[LOG] virtualizing first={panel.FirstRealizedIndex} last={panel.LastRealizedIndex} count={ViewModel.Lines?.Count ?? 0}");
            },
            DispatcherPriority.Loaded);
    }

    public void OnLogAppended(GameLogBuffer buffer)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnLogAppended(buffer));
            return;
        }

        if (!ViewModel.RefreshFromBuffer(buffer))
            return;

        RefreshCounters();
        ScrollToEndIfNeeded();
    }

    private void ScrollToEndIfNeeded()
    {
        if (!ViewModel.AutoScroll)
            return;
        Dispatcher.UIThread.Post(() =>
        {
            if (ViewModel.AutoScroll)
                PanBack.ScrollToEnd();
        });
    }

    private SelectableTextBlock CreateLogLine(GameLogEntry? line)
    {
        return line switch
        {
            null => new SelectableTextBlock
            {
                IsVisible = false
            },
            _ => new SelectableTextBlock
            {
                Text = line.Text,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily(
                    "Consolas,Menlo,DejaVu Sans Mono,Liberation Mono,monospace"),
                FontSize = 14d,
                Foreground = GetLevelBrush(line.Level)
            }
        };
    }

    private IBrush GetLevelBrush(GameLogLevel level)
    {
        var key = level switch
        {
            GameLogLevel.Debug => "ColorBrushDebug",
            GameLogLevel.Warn => "ColorBrushWarn",
            GameLogLevel.Error => "ColorBrushError",
            GameLogLevel.Fatal => "ColorBrushFatal",
            _ => ThemeService.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo"
        };
        return ContainerTheme.Brush(this, key, Brushes.White);
    }

    private void RefreshCounters()
    {
        PanLogCard.Inlines.Clear();
        PanLogCard.Inlines.Add(new Run(Lang.Text("LogPage.Title")));
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(ViewModel.DebugCountText, "ColorBrushDebug");
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(
            ViewModel.InfoCountText,
            ThemeService.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo");
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(ViewModel.WarnCountText, "ColorBrushWarn");
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(ViewModel.ErrorCountText, "ColorBrushError");
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(ViewModel.FatalCountText, "ColorBrushFatal");
        return;

        void AddCounter(string text, string brushKey)
        {
            PanLogCard.Inlines.Add(
                new Run(text)
                {
                    Foreground = ContainerTheme.Brush(this, brushKey, Brushes.White)
                });
        }
    }

    private void SubscribeCurrentSession(GameSession session)
    {
        if (ReferenceEquals(_subscribedSession, session))
            return;
        UnsubscribeCurrentSession();
        _subscribedSession = session;
        session.Exited += OnGameExit;
        session.StateChanged += OnSessionStateChanged;
    }

    private void UnsubscribeCurrentSession()
    {
        if (_subscribedSession is null)
            return;
        _subscribedSession.Exited -= OnGameExit;
        _subscribedSession.StateChanged -= OnSessionStateChanged;
        _subscribedSession = null;
    }

    private void OnSessionStateChanged(object? sender, GameSessionStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ViewModel.RefreshActionAvailability());
    }

    private void OnGameExit(object? sender, GameSessionExitedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => ViewModel.OnGameExit(e));
    }
}