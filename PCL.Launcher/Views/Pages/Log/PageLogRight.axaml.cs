using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Theme;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Log;

/// <summary>
///     游戏实时日志页右栏：按级别着色渲染当前日志，并提供导出、清空、结束进程与线程快照操作。
/// </summary>
public partial class PageLogRight : MyPageRight, IRefreshable
{
    private bool _refreshed;
    private PageLogLeft.LogBuffer? _renderedBuffer;
    private int _renderedCount;
    private PageLogLeft.LogLine? _renderedLastLine;

    private GameWatcher.Watcher? _subscribedLog;

    public PageLogRight()
    {
        InitializeComponent();
        PanScroll = PanBack;
        Current = this;

        BtnOperationExport.Click += (_, _) => ExportLog();
        BtnOperationClear.Click += (_, _) => ClearLog();
        BtnOperationKill.Click += (_, _) => KillProcess();
        BtnOperationExportStackDump.Click += (_, _) => ExportStackDump();
        SliderMaxLog.Change += OnMaxLogChanged;
        SliderMaxLog.getHintText = GetMaxLogHintText;
        DetachedFromVisualTree += (_, _) => UnsubscribeCurrentLog();
    }

    public static PageLogRight? Current { get; private set; }

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

        var left = Left;
        var hasLog = left is { CurrentUuid: > 0, CurrentLog: not null };
        PanAllBack.IsVisible = hasLog;
        CardOperation.IsVisible = hasLog;
        LabEmpty.IsVisible = !hasLog;
        if (!hasLog)
        {
            PanLog.Children.Clear();
            PanLogCard.Inlines.Clear();
            _renderedBuffer = null;
            _renderedCount = 0;
            _renderedLastLine = null;
            return;
        }

        SubscribeCurrentLog(left!.CurrentLog!);
        SliderMaxLog.Value = Math.Clamp(Config.System.MaxGameLog, 0, SliderMaxLog.MaxValue);
        RefreshOperationButtons();
        RefreshCounters();

        var buffer = left.LogBuffers.GetValueOrDefault(left.CurrentUuid);
        RenderBuffer(buffer);
    }

    public void OnLogAppended(PageLogLeft.LogBuffer buffer)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnLogAppended(buffer));
            return;
        }

        var left = Left;
        if (left is null || left.CurrentUuid <= 0)
            return;
        if (!ReferenceEquals(left.LogBuffers.GetValueOrDefault(left.CurrentUuid), buffer))
            return;

        AppendMissingLines(buffer);
        RefreshCounters();
        if (CheckAutoScroll.Checked == true)
            PanBack.ScrollToEnd();
    }

    private void RenderBuffer(PageLogLeft.LogBuffer? buffer)
    {
        PanLog.Children.Clear();
        _renderedBuffer = buffer;
        _renderedCount = 0;
        _renderedLastLine = null;
        AppendMissingLines(buffer);
        if (CheckAutoScroll.Checked == true)
            PanBack.ScrollToEnd();
    }

    private void AppendMissingLines(PageLogLeft.LogBuffer? buffer)
    {
        if (buffer is null)
            return;
        if (!ReferenceEquals(_renderedBuffer, buffer))
        {
            PanLog.Children.Clear();
            _renderedBuffer = buffer;
            _renderedCount = 0;
            _renderedLastLine = null;
        }

        if (buffer.Lines.Count == _renderedCount)
        {
            if (buffer.Lines.Count == 0
                || ReferenceEquals(buffer.Lines[^1], _renderedLastLine))
                return;
            PanLog.Children.Clear();
            _renderedCount = 0;
            _renderedLastLine = null;
        }
        else if (_renderedCount > buffer.Lines.Count)
        {
            PanLog.Children.Clear();
            _renderedCount = 0;
            _renderedLastLine = null;
        }

        for (var i = _renderedCount; i < buffer.Lines.Count; i++)
            PanLog.Children.Add(CreateLogLine(buffer.Lines[i]));
        _renderedCount = buffer.Lines.Count;
        _renderedLastLine = buffer.Lines.Count > 0 ? buffer.Lines[^1] : null;

        var max = (long)PageLogLeft.GetMaxLogCount();
        while (PanLog.Children.Count > max)
            PanLog.Children.RemoveAt(0);
    }

    private TextBlock CreateLogLine(PageLogLeft.LogLine line)
    {
        var text = new TextBlock
        {
            Text = line.Text,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas,Menlo,DejaVu Sans Mono,monospace"),
            FontSize = 13d,
            Foreground = GetLevelBrush(line.Level)
        };
        return text;
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
        AddCounter(Lang.Text("LogPage.Level.Debug"), "ColorBrushDebug", log => log.countDebug);
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(
            Lang.Text("LogPage.Level.Info"),
            ThemeService.IsDarkMode ? "ColorBrushInfoDark" : "ColorBrushInfo",
            log => log.countInfo);
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(Lang.Text("LogPage.Level.Warn"), "ColorBrushWarn", log => log.countWarn);
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(Lang.Text("LogPage.Level.Error"), "ColorBrushError", log => log.countError);
        PanLogCard.Inlines.Add(new Run(" | "));
        AddCounter(Lang.Text("LogPage.Level.Fatal"), "ColorBrushFatal", log => log.countFatal);
        return;

        void AddCounter(string label, string brushKey, Func<GameWatcher.Watcher, uint> selector)
        {
            var log = Left?.CurrentLog;
            var value = log is null ? 0u : selector(log);
            PanLogCard.Inlines.Add(
                new Run($"{value} {label}")
                {
                    Foreground = ContainerTheme.Brush(this, brushKey, Brushes.White)
                });
        }
    }

    private void RefreshOperationButtons()
    {
        var log = Left?.CurrentLog;
        if (log is null)
        {
            BtnOperationKill.IsEnabled = false;
            BtnOperationExportStackDump.IsEnabled = false;
            return;
        }

        BtnOperationKill.IsEnabled = !log.gameProcess.HasExited;
        BtnOperationExportStackDump.IsEnabled =
            !log.gameProcess.HasExited && !string.IsNullOrWhiteSpace(log.jStackPath);
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
        Dispatcher.UIThread.Post(() =>
        {
            BtnOperationKill.IsEnabled = false;
            BtnOperationExportStackDump.IsEnabled = false;
        });
    }

    #endregion

    #region 操作卡片

    private void OnMaxLogChanged(object sender, bool user)
    {
        Config.System.MaxGameLog = SliderMaxLog.Value;
    }

    private static object GetMaxLogHintText(object value)
    {
        return value switch
        {
            _ when (int)value <= 5 => ((int)value * 10 + 50).ToString(CultureInfo.CurrentCulture),
            _ when (int)value <= 13 => ((int)value * 50 - 150).ToString(CultureInfo.CurrentCulture),
            _ when (int)value <= 28 =>
                ((int)value * 100 - 800).ToString(CultureInfo.CurrentCulture),
            _ => Lang.Text("LogPage.MaxLines.Unlimited")
        };
    }

    private void ExportLog()
    {
        var log = Left?.CurrentLog;
        if (log is null)
            return;

        var savePath = SystemDialogs.SelectSaveFile(
            Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.Export.GameLog.FileName", log.version?.Name ?? ""),
            Lang.Text("LogPage.Export.GameLog.Filter"));
        if (savePath.Length < 3)
            return;

        try
        {
            File.WriteAllLines(savePath, log.fullLog);
            HintService.Show(Hints.Success(Lang.Text("LogPage.Export.Success")));
            Basics.OpenPath(savePath);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("LogPage.Error.OperationFailed"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("LogPage.Error.OperationFailed"));
        }
    }

    private void ClearLog()
    {
        var left = Left;
        if (left is null || left.CurrentUuid <= 0)
            return;
        if (!left.LogBuffers.TryGetValue(left.CurrentUuid, out var buffer))
            return;
        buffer.Lines.Clear();
        RenderBuffer(buffer);
    }

    private void KillProcess()
    {
        var log = Left?.CurrentLog;
        if (log is null)
            return;
        if (log.State > GameWatcher.Watcher.MinecraftState.Running)
            return;

        log.Kill();
        HintService.Show(
            Hints.Success(
                Lang.Text("LogPage.Action.GameClosed", log.version?.Name ?? string.Empty)));
    }

    private void ExportStackDump()
    {
        var log = Left?.CurrentLog;
        if (log is null)
            return;

        var formattedDate = DateTime
            .Now.ToString("G", CultureInfo.InvariantCulture)
            .Replace("/", "-")
            .Replace(":", ".")
            .Replace(" ", "_");
        var savePath = SystemDialogs.SelectSaveFile(
            Lang.Text("LogPage.Export.SelectLocation"),
            Lang.Text("LogPage.ExportStack.FileName", formattedDate),
            Lang.Text("LogPage.ExportStack.Filter"));
        if (savePath.Length < 3)
            return;

        HintService.Show(Hints.Info(Lang.Text("LogPage.ExportStack.Progress")));
        BtnOperationExportStackDump.IsEnabled = false;
        LauncherCommon.RunInNewThread(
            () =>
            {
                try
                {
                    var dump = log.ExportStackDump();
                    File.WriteAllLines(savePath, dump);
                    LauncherCommon.RunInUi(() =>
                    {
                        HintService.Show(Hints.Success(Lang.Text("LogPage.ExportStack.Success")));
                        BtnOperationExportStackDump.IsEnabled = true;
                    });
                    Basics.OpenPath(savePath);
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        Lang.Text("LogPage.Error.OperationFailed"),
                        LauncherLogLevel.Feedback,
                        userSummary: Lang.Text("LogPage.Error.OperationFailed"));
                    LauncherCommon.RunInUi(() => BtnOperationExportStackDump.IsEnabled = true);
                }
            },
            "Game Log Stack Dump",
            ThreadPriority.BelowNormal);
    }

    #endregion
}