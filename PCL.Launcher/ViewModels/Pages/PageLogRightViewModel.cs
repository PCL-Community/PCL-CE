using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using PCL.Views.Pages.Log;

namespace PCL;

/// <summary>游戏实时日志页右栏的视图模型：日志行、计数、自动滚动与最大行数。</summary>
public partial class PageLogRightViewModel : ViewModelBase
{
    private PageLogLeft? _left;

    [ObservableProperty] public partial bool AutoScroll { get; set; } = true;

    [ObservableProperty]
    public partial string DebugCountText { get; set; } = $"0 {Lang.Text("LogPage.Level.Debug")}";

    [ObservableProperty]
    public partial string ErrorCountText { get; set; } = $"0 {Lang.Text("LogPage.Level.Error")}";

    [ObservableProperty]
    public partial string FatalCountText { get; set; } = $"0 {Lang.Text("LogPage.Level.Fatal")}";

    [ObservableProperty] public partial bool HasLog { get; set; }

    [ObservableProperty]
    public partial string InfoCountText { get; set; } = $"0 {Lang.Text("LogPage.Level.Info")}";

    [ObservableProperty] public partial bool IsExportStackDumpEnabled { get; set; }

    [ObservableProperty] public partial bool IsKillEnabled { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<PageLogLeft.LogLine>? Lines { get; set; }

    [ObservableProperty]
    public partial int MaxLogLines { get; set; } = Math.Clamp(Config.System.MaxGameLog, 0, 29);

    [ObservableProperty]
    public partial string WarnCountText { get; set; } = $"0 {Lang.Text("LogPage.Level.Warn")}";

    partial void OnMaxLogLinesChanged(int value)
    {
        if (value >= 0 && Config.System.MaxGameLog != value)
            Config.System.MaxGameLog = value;
    }

    /// <summary>接入左栏当前选中的日志缓冲区。</summary>
    public void Load(PageLogLeft? left)
    {
        _left = left;
        HasLog = left is { CurrentUuid: > 0, CurrentLog: not null };
        if (!HasLog)
        {
            Lines = null;
            IsKillEnabled = false;
            IsExportStackDumpEnabled = false;
            RefreshCounters();
            return;
        }

        MaxLogLines = Math.Clamp(Config.System.MaxGameLog, 0, 29);
        Lines = left!.LogBuffers.GetValueOrDefault(left.CurrentUuid)?.Lines;
        RefreshOperationButtons();
        RefreshCounters();
    }

    /// <summary>左栏追加日志后刷新计数；调用方负责滚动。</summary>
    public bool OnLogAppended(PageLogLeft.LogBuffer buffer)
    {
        var left = _left;
        if (left is null || left.CurrentUuid <= 0)
            return false;
        if (!ReferenceEquals(left.LogBuffers.GetValueOrDefault(left.CurrentUuid), buffer))
            return false;

        RefreshCounters();
        return true;
    }

    public void RefreshOperationButtons()
    {
        var log = _left?.CurrentLog;
        if (log is null)
        {
            IsKillEnabled = false;
            IsExportStackDumpEnabled = false;
            return;
        }

        IsKillEnabled = !log.gameProcess.HasExited;
        IsExportStackDumpEnabled =
            !log.gameProcess.HasExited && !string.IsNullOrWhiteSpace(log.jStackPath);
    }

    public void RefreshCounters()
    {
        var log = _left?.CurrentLog;
        var fatal = log?.countFatal ?? 0u;
        var error = log?.countError ?? 0u;
        var warn = log?.countWarn ?? 0u;
        var info = log?.countInfo ?? 0u;
        var debug = log?.countDebug ?? 0u;
        FatalCountText = $"{fatal} {Lang.Text("LogPage.Level.Fatal")}";
        ErrorCountText = $"{error} {Lang.Text("LogPage.Level.Error")}";
        WarnCountText = $"{warn} {Lang.Text("LogPage.Level.Warn")}";
        InfoCountText = $"{info} {Lang.Text("LogPage.Level.Info")}";
        DebugCountText = $"{debug} {Lang.Text("LogPage.Level.Debug")}";
    }

    public void OnGameExit()
    {
        IsKillEnabled = false;
        IsExportStackDumpEnabled = false;
    }

    /// <summary>按级别与最大行数过滤日志；当前页面未暴露级别筛选 UI，保留给命令/宿主复用。</summary>
    public static IReadOnlyList<PageLogLeft.LogLine> FilterLines(
        IEnumerable<PageLogLeft.LogLine> source,
        GameLogLevel? level = null)
    {
        var filtered = level is null
            ? source
            : source.Where(line => line.Level == level);
        var max = PageLogLeft.GetMaxLogCount();
        return max == ulong.MaxValue
            ? filtered.ToList()
            : filtered.TakeLast((int)Math.Min(max, int.MaxValue)).ToList();
    }

    /// <summary>清空当前缓冲区；返回是否确实清空了内容。</summary>
    public bool ClearLog()
    {
        var left = _left;
        if (left is null || left.CurrentUuid <= 0)
            return false;
        if (!left.LogBuffers.TryGetValue(left.CurrentUuid, out var buffer))
            return false;
        buffer.Lines.Clear();
        return true;
    }

    public async Task ExportLogAsync()
    {
        var log = _left?.CurrentLog;
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
            await Task.Run(() => File.WriteAllLines(savePath, log.fullLog));
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

    public void KillProcess()
    {
        var log = _left?.CurrentLog;
        if (log is null)
            return;
        if (log.State > GameWatcher.Watcher.MinecraftState.Running)
            return;

        log.Kill();
        HintService.Show(
            Hints.Success(
                Lang.Text("LogPage.Action.GameClosed", log.version?.Name ?? string.Empty)));
    }

    public async Task ExportStackDumpAsync()
    {
        var log = _left?.CurrentLog;
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
        IsExportStackDumpEnabled = false;
        try
        {
            var dump = await Task.Run(log.ExportStackDump);
            await Task.Run(() => File.WriteAllLines(savePath, dump));
            HintService.Show(Hints.Success(Lang.Text("LogPage.ExportStack.Success")));
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
        finally
        {
            RefreshOperationButtons();
        }
    }

    public static object GetMaxLogHintText(object value)
    {
        return value switch
        {
            _ when (int)value <= 5 => ((int)value * 10 + 50).ToString(
                CultureInfo.CurrentCulture),
            _ when (int)value <= 13 => ((int)value * 50 - 150).ToString(
                CultureInfo.CurrentCulture),
            _ when (int)value <= 28 => ((int)value * 100 - 800).ToString(
                CultureInfo.CurrentCulture),
            _ => Lang.Text("LogPage.MaxLines.Unlimited")
        };
    }
}