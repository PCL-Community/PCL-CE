using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Log;

/// <summary>
///     游戏实时日志页右栏：按级别着色渲染当前日志，并提供导出、清空、结束进程与线程快照操作。
///     正文使用可选中文本与虚拟化列表，避免长日志一次性创建全部控件。
/// </summary>
public partial class PageLogRight : MyPageRight, IRefreshable
{
    private bool _refreshed;

    private GameWatcher.Watcher? _subscribedLog;

    public PageLogRight()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Current = this;
        DataContext = ViewModel;
        PanLog.ItemTemplate =
            new FuncDataTemplate<PageLogLeft.LogLine>((line, _) => CreateLogLine(line));

        BtnOperationExport.Click += (_, _) => _ = ViewModel.ExportLogAsync();
        BtnOperationClear.Click += (_, _) =>
        {
            if (ViewModel.ClearLog())
                Dispatcher.UIThread.Post(() => PanBack.ScrollToHome());
        };
        BtnOperationKill.Click += (_, _) => ViewModel.KillProcess();
        BtnOperationExportStackDump.Click += (_, _) => _ = ViewModel.ExportStackDumpAsync();
        SliderMaxLog.Change += (_, _) => ViewModel.MaxLogLines = SliderMaxLog.Value;
        SliderMaxLog.getHintText = PageLogRightViewModel.GetMaxLogHintText;
        DetachedFromVisualTree += (_, _) =>
        {
            _refreshed = false;
            UnsubscribeCurrentLog();
        };
    }

    public static PageLogRight? Current { get; private set; }

    public PageLogRightViewModel ViewModel { get; } = new();

    private static PageLogLeft? Left => PageLogLeft.Current;

    public void Refresh()
    {
        ReloadContent();
    }

    public override void OnNavigatedTo(object? parameter)
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
        UnsubscribeCurrentLog();
    }

    protected override void RefreshLocalization()
    {
        ReloadContent();
    }

    #region 渲染

    public void ReloadContent()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ReloadContent);
            return;
        }

        ViewModel.Load(Left);
        RefreshCounters();
        if (ViewModel.HasLog)
            SubscribeCurrentLog(Left!.CurrentLog!);
        else
            UnsubscribeCurrentLog();
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

    public void OnLogAppended(PageLogLeft.LogBuffer buffer)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnLogAppended(buffer));
            return;
        }

        if (!ViewModel.OnLogAppended(buffer))
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

    private SelectableTextBlock CreateLogLine(PageLogLeft.LogLine line)
    {
        return new SelectableTextBlock
        {
            Text = line.Text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,Menlo,DejaVu Sans Mono,monospace"),
            FontSize = 14d,
            Foreground = GetLevelBrush(line.Level)
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

    private void SubscribeCurrentLog(GameWatcher.Watcher log)
    {
        if (ReferenceEquals(_subscribedLog, log))
            return;
        UnsubscribeCurrentLog();
        _subscribedLog = log;
        log.GameExit += OnGameExit;
    }

    private void UnsubscribeCurrentLog()
    {
        if (_subscribedLog is null)
            return;
        _subscribedLog.GameExit -= OnGameExit;
        _subscribedLog = null;
    }

    private void OnGameExit()
    {
        Dispatcher.UIThread.Post(() => ViewModel.OnGameExit());
    }

    #endregion
}