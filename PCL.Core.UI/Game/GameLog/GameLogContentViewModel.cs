using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.UI.Hints;
using PCL.Core.ViewModel;
using HintPresets = PCL.Core.UI.Hints.Hints;

namespace PCL.Core.UI.Game.Log;

/// <summary>游戏实时日志页右栏：日志行、统计、自动滚动与日志/运行栈操作。</summary>
public partial class GameLogContentViewModel : ViewModelBase
{
    private GameLogListViewModel? _list;

    private GameLogCounters _statistics;

    [ObservableProperty] public partial bool AutoScroll { get; set; } = true;

    public string DebugCountText => $"{_statistics.Debug} {Lang.Text("LogPage.Level.Debug")}";

    public string ErrorCountText => $"{_statistics.Error} {Lang.Text("LogPage.Level.Error")}";

    public string FatalCountText => $"{_statistics.Fatal} {Lang.Text("LogPage.Level.Fatal")}";

    public string InfoCountText => $"{_statistics.Info} {Lang.Text("LogPage.Level.Info")}";

    public string WarnCountText => $"{_statistics.Warn} {Lang.Text("LogPage.Level.Warn")}";

    [ObservableProperty] public partial bool HasLog { get; set; }

    [ObservableProperty] public partial bool IsExportStackDumpEnabled { get; set; }

    [ObservableProperty] public partial bool IsKillEnabled { get; set; }

    [ObservableProperty] public partial ObservableCollection<GameLogEntry>? Lines { get; set; }

    [ObservableProperty]
    public partial int MaxLogLines { get; set; } = Math.Clamp(Config.System.MaxGameLog, 0, 29);

    partial void OnMaxLogLinesChanged(int value)
    {
        if (value >= 0 && Config.System.MaxGameLog != value)
            Config.System.MaxGameLog = value;
    }

    /// <summary>绑定左栏的会话列表，并切换到当前选中的日志缓冲区。</summary>
    public void Bind(GameLogListViewModel? list)
    {
        _list = list;
        HasLog = list is { CurrentUuid: > 0, CurrentSession: not null };

        if (!HasLog)
        {
            Lines = null;
            IsKillEnabled = false;
            IsExportStackDumpEnabled = false;
            RefreshStatistics();
            return;
        }

        MaxLogLines = Math.Clamp(Config.System.MaxGameLog, 0, 29);
        Lines = list!.Buffers.GetValueOrDefault(list.CurrentUuid)?.Lines;
        RefreshActionAvailability();
        RefreshStatistics();
    }

    /// <summary>左栏追加日志后刷新统计；调用方负责滚动。</summary>
    public bool RefreshFromBuffer(GameLogBuffer buffer)
    {
        var list = _list;
        if (list is null || list.CurrentUuid <= 0)
            return false;
        if (!ReferenceEquals(list.Buffers.GetValueOrDefault(list.CurrentUuid), buffer))
            return false;

        RefreshStatistics();
        return true;
    }

    public void RefreshActionAvailability()
    {
        var session = _list?.CurrentSession;
        if (session is null)
        {
            IsKillEnabled = false;
            IsExportStackDumpEnabled = false;
            return;
        }

        IsKillEnabled = session.CanTerminate;
        IsExportStackDumpEnabled = session.CanCaptureStackDump;
    }

    public void RefreshStatistics()
    {
        _statistics = _list?.CurrentSession?.Counters ?? default;
        OnPropertyChanged(nameof(FatalCountText));
        OnPropertyChanged(nameof(ErrorCountText));
        OnPropertyChanged(nameof(WarnCountText));
        OnPropertyChanged(nameof(InfoCountText));
        OnPropertyChanged(nameof(DebugCountText));
    }

    public void OnGameExit()
    {
        IsKillEnabled = false;
        IsExportStackDumpEnabled = false;
    }

    /// <summary>按级别与最大行数过滤日志；当前页面未暴露级别筛选 UI，保留给命令/宿主复用。</summary>
    public static IReadOnlyList<GameLogEntry> FilterByLevel(
        IEnumerable<GameLogEntry> source,
        GameLogLevel? level = null)
    {
        var filtered = level is null
            ? source
            : source.Where(line => line.Level == level);
        var max = GameLogListViewModel.GetMaxLogCount();
        return max == ulong.MaxValue
            ? filtered.ToList()
            : filtered.TakeLast((int)Math.Min(max, int.MaxValue)).ToList();
    }

    /// <summary>清空当前缓冲区；返回是否确实清空了内容。</summary>
    public bool ClearCurrentBuffer()
    {
        var list = _list;
        if (list is null || list.CurrentUuid <= 0)
            return false;
        if (!list.Buffers.TryGetValue(list.CurrentUuid, out var buffer))
            return false;

        buffer.Lines.Clear();
        return true;
    }

    public Task ExportLogAsync()
    {
        var session = _list?.CurrentSession;
        return session is null ? Task.CompletedTask : GameLogExportService.ExportLogAsync(session);
    }

    public void TerminateCurrentSession()
    {
        var session = _list?.CurrentSession;
        if (session is null)
            return;
        if ((int)session.State > (int)GameSessionState.Running)
            return;

        session.Kill();
        HintService.Show(
            HintPresets.Success(
                Lang.Text("LogPage.Action.GameClosed", session.Metadata.InstanceName)));
    }

    public async Task ExportStackDumpAsync()
    {
        var session = _list?.CurrentSession;
        if (session is null)
            return;

        IsExportStackDumpEnabled = false;
        try
        {
            await GameLogExportService.ExportStackDumpAsync(session);
        }
        finally
        {
            RefreshActionAvailability();
        }
    }

    public static object FormatMaxLogHint(object value)
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