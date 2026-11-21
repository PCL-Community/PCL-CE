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
        if (value < 0 || Config.System.MaxGameLog == value)
            return;

        Config.System.MaxGameLog = value;
        _list?.TrimAllBuffers();
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

    public void OnGameExit(GameSessionExitedEventArgs args)
    {
        IsKillEnabled = false;
        IsExportStackDumpEnabled = false;

        if (args.Reason == GameExitReason.Canceled)
            HintService.Show(
                HintPresets.Success(
                    Lang.Text(
                        "LogPage.Action.GameClosed",
                        args.Session.Metadata.InstanceName)));
    }

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
        if (session is null || !session.CanTerminate)
            return;

        IsKillEnabled = false;
        session.Kill();
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
        var configured = Convert.ToInt32(value, CultureInfo.InvariantCulture);
        var max = GameLogLimits.GetMaxLineCount(configured);
        return max == ulong.MaxValue
            ? Lang.Text("LogPage.MaxLines.Unlimited")
            : max.ToString(CultureInfo.CurrentCulture);
    }
}