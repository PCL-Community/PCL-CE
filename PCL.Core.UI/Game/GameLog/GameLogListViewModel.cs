using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Watcher;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Threading;
using PCL.Core.ViewModel;
using HintPresets = PCL.Core.UI.Hints.Hints;

namespace PCL.Core.UI.Game.Log;

/// <summary>游戏实时日志页左栏：维护会话列表、日志缓冲区与当前选择。</summary>
public partial class GameLogListViewModel : ViewModelBase
{
    private const int DefaultVisibleLines = 500;
    private const int MaxBatchSize = 200;

    private static int _nextUuid;

    private readonly ConcurrentQueue<(GameSession Session, GameLogLineEventArgs Args)>
        _pendingLines = new();

    private int _flushScheduled;

    public Dictionary<int, GameLogBuffer> Buffers { get; } = [];

    public List<KeyValuePair<int, GameSession>> ShownSessions { get; } = [];

    [ObservableProperty] public partial GameSession? CurrentSession { get; set; }

    [ObservableProperty] public partial int CurrentUuid { get; set; } = -1;

    [ObservableProperty] public partial bool HasLogs { get; set; }

    public string InstancesTitle => Lang.Text("LogPage.Left.InstancesTitle");

    /// <summary>登记一个游戏会话并切换到它的日志。可在任意线程调用。</summary>
    public void AttachSession(GameSession session)
    {
        if (!UiDispatcher.CheckAccess())
        {
            UiDispatcher.Post(() => AttachSession(session));
            return;
        }

        if (ShownSessions.Any(item => ReferenceEquals(item.Value, session)))
            return;

        var uuid = Interlocked.Increment(ref _nextUuid);
        ShownSessions.Add(new KeyValuePair<int, GameSession>(uuid, session));

        var buffer = new GameLogBuffer();
        foreach (var entry in session.History)
            buffer.Lines.Add(new GameLogEntry(entry.Text, entry.Level));
        TrimBuffer(buffer);
        Buffers[uuid] = buffer;
        session.LogLineReceived += OnLogLineReceived;

        HasLogs = ShownSessions.Count > 0;
        SelectionChange(uuid);
    }

    /// <summary>会话列表或当前选择发生变化；由页面重建列表并刷新右栏。</summary>
    public event Action? Changed;

    /// <summary>当前会话产生新日志行；由页面转交给右栏滚动渲染。</summary>
    public event Action<GameLogBuffer>? LogAppended;

    private void OnLogLineReceived(object? sender, GameLogLineEventArgs e)
    {
        if (sender is not GameSession session)
            return;

        _pendingLines.Enqueue((session, e));
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
            UiDispatcher.Post(FlushPendingLines);
    }

    private void FlushPendingLines()
    {
        var changed = new HashSet<GameLogBuffer>();
        try
        {
            var processed = 0;
            while (processed < MaxBatchSize && _pendingLines.TryDequeue(out var item))
            {
                processed++;
                AddPendingLine(item.Session, item.Args, changed);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
            if (!_pendingLines.IsEmpty
                && Interlocked.Exchange(ref _flushScheduled, 1) == 0)
                UiDispatcher.Post(FlushPendingLines);
        }

        if (CurrentUuid > 0
            && Buffers.TryGetValue(CurrentUuid, out var current)
            && changed.Contains(current))
            LogAppended?.Invoke(current);
    }

    private void AddPendingLine(
        GameSession session,
        GameLogLineEventArgs args,
        HashSet<GameLogBuffer> changed)
    {
        var uuid = -1;
        foreach (var item in ShownSessions.Where(item => ReferenceEquals(item.Value, session)))
        {
            uuid = item.Key;
            break;
        }

        if (uuid < 0 || !Buffers.TryGetValue(uuid, out var buffer))
            return;

        buffer.Lines.Add(new GameLogEntry(args.Text, args.Level));
        TrimBuffer(buffer);
        changed.Add(buffer);
    }

    public void TrimAllBuffers()
    {
        if (!UiDispatcher.CheckAccess())
        {
            UiDispatcher.Post(TrimAllBuffers);
            return;
        }

        var max = GetMaxLogCount();
        foreach (var buffer in Buffers.Values)
            TrimBuffer(buffer, max);
    }

    private static void TrimBuffer(GameLogBuffer buffer)
    {
        TrimBuffer(buffer, GetMaxLogCount());
    }

    private static void TrimBuffer(GameLogBuffer buffer, ulong max)
    {
        while ((ulong)buffer.Lines.Count > max)
            buffer.Lines.RemoveAt(0);
    }

    public static ulong GetMaxLogCount()
    {
        return GetMaxLogCount(Config.System.MaxGameLog);
    }

    internal static ulong GetMaxLogCount(int maxGameLog)
    {
        return GameLogLimits.GetMaxLineCount(maxGameLog);
    }

    public void SelectionChange(int uuid)
    {
        if (!UiDispatcher.CheckAccess())
        {
            UiDispatcher.Post(() => SelectionChange(uuid));
            return;
        }

        if (uuid <= 0)
        {
            CurrentUuid = -1;
            CurrentSession = null;
        }
        else
        {
            foreach (var item in ShownSessions.Where(item => item.Key == uuid))
            {
                CurrentUuid = uuid;
                CurrentSession = item.Value;
                break;
            }
        }

        Changed?.Invoke();
    }

    public void RemoveItem(int uuid)
    {
        if (!UiDispatcher.CheckAccess())
        {
            UiDispatcher.Post(() => RemoveItem(uuid));
            return;
        }

        for (var i = ShownSessions.Count - 1; i >= 0; i--)
        {
            var item = ShownSessions[i];
            if (item.Key != uuid)
                continue;

            item.Value.LogLineReceived -= OnLogLineReceived;
            ShownSessions.RemoveAt(i);
            Buffers.Remove(uuid);
            HasLogs = ShownSessions.Count > 0;

            if (CurrentUuid == uuid)
            {
                if (ShownSessions.Count == 0)
                    SelectionChange(-1);
                else
                    SelectionChange(ShownSessions[Math.Min(i, ShownSessions.Count - 1)].Key);
            }
            else
            {
                Changed?.Invoke();
            }

            break;
        }
    }

    /// <summary>按当前会话列表生成左栏条目。</summary>
    public IReadOnlyList<GameLogSessionEntry> BuildEntries()
    {
        var result = new List<GameLogSessionEntry>(ShownSessions.Count);
        foreach (var (uuid, session) in ShownSessions)
            result.Add(new GameLogSessionEntry(uuid, session));

        return result;
    }

    /// <summary>首次显示日志列表时给出最大行数提示。</summary>
    public static void ShowMaxLinesHintIfNeeded()
    {
        if (States.Hint.MaxGameLog)
            return;

        States.Hint.MaxGameLog = true;
        HintService.Show(HintPresets.Info(Lang.Text("LogPage.MaxLines.Hint", DefaultVisibleLines)));
    }
}