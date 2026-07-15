namespace PCL.Application.Launching;

public enum GameSessionState
{
    Starting,
    Running,
    Exited,
    Crashed,
    Terminated
}

public enum GameProcessOutputChannel
{
    StandardOutput,
    StandardError,
    Launcher
}

public sealed record GameSessionSnapshot(
    Guid SessionId,
    string InstanceId,
    int ProcessId,
    GameSessionState State,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int? ExitCode,
    long LastSequence,
    string? LanAddress = null);

public sealed record GameLaunchEvent(
    long Sequence,
    Guid SessionId,
    string Kind,
    DateTimeOffset Timestamp,
    GameSessionSnapshot Session);

public sealed record GameProcessOutput(
    long Sequence,
    Guid SessionId,
    GameProcessOutputChannel Stream,
    string Text,
    DateTimeOffset Timestamp);

public interface IGameSessionRegistry
{
    event Action<GameLaunchEvent>? LaunchEventPublished;
    event Action<GameProcessOutput>? ProcessOutputPublished;
    IReadOnlyList<GameSessionSnapshot> ListSessions();
    bool TryGetSession(Guid sessionId, out GameSessionSnapshot? session);
    IReadOnlyList<GameProcessOutput> ReadOutput(Guid sessionId, long afterSequence, int maximumCount = 256);
}

public sealed class GameSessionRegistry : IGameSessionRegistry
{
    private const int MaximumOutputHistoryPerSession = 4096;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, SessionRecord> _sessions = [];
    private long _sequence;

    public static GameSessionRegistry Shared { get; } = new();

    public event Action<GameLaunchEvent>? LaunchEventPublished;
    public event Action<GameProcessOutput>? ProcessOutputPublished;

    public GameSessionSnapshot Start(string instanceId, int processId, DateTimeOffset? startedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);

        GameLaunchEvent launchEvent;
        lock (_gate)
        {
            Guid id = Guid.NewGuid();
            long sequence = NextSequence();
            GameSessionSnapshot snapshot = new(
                id,
                instanceId.Trim(),
                processId,
                GameSessionState.Running,
                startedAt ?? DateTimeOffset.UtcNow,
                null,
                null,
                sequence);
            _sessions.Add(id, new SessionRecord(snapshot));
            launchEvent = new GameLaunchEvent(sequence, id, "started", DateTimeOffset.UtcNow, snapshot);
        }
        Publish(launchEvent);
        return launchEvent.Session;
    }

    public bool Complete(Guid sessionId, int exitCode, bool terminated = false, DateTimeOffset? endedAt = null)
    {
        GameLaunchEvent? launchEvent = null;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out SessionRecord? record) || IsTerminal(record.Snapshot.State))
                return false;
            long sequence = NextSequence();
            GameSessionState state = terminated
                ? GameSessionState.Terminated
                : exitCode == 0 ? GameSessionState.Exited : GameSessionState.Crashed;
            record.Snapshot = record.Snapshot with
            {
                State = state,
                EndedAt = endedAt ?? DateTimeOffset.UtcNow,
                ExitCode = exitCode,
                LastSequence = sequence
            };
            launchEvent = new GameLaunchEvent(sequence, sessionId, state switch
            {
                GameSessionState.Crashed => "crashed",
                GameSessionState.Terminated => "terminated",
                _ => "exited"
            }, DateTimeOffset.UtcNow, record.Snapshot);
        }
        Publish(launchEvent);
        return true;
    }

    public bool PublishLanAddress(Guid sessionId, string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        GameLaunchEvent? launchEvent = null;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out SessionRecord? record) || IsTerminal(record.Snapshot.State))
                return false;
            long sequence = NextSequence();
            record.Snapshot = record.Snapshot with { LanAddress = address.Trim(), LastSequence = sequence };
            launchEvent = new GameLaunchEvent(sequence, sessionId, "lan-detected", DateTimeOffset.UtcNow, record.Snapshot);
        }
        Publish(launchEvent);
        return true;
    }

    public bool PublishOutput(Guid sessionId, GameProcessOutputChannel stream, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        GameProcessOutput? output = null;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out SessionRecord? record))
                return false;
            long sequence = NextSequence();
            output = new GameProcessOutput(sequence, sessionId, stream, text, DateTimeOffset.UtcNow);
            record.Output.Enqueue(output);
            while (record.Output.Count > MaximumOutputHistoryPerSession)
                record.Output.Dequeue();
            record.Snapshot = record.Snapshot with { LastSequence = sequence };
        }
        ProcessOutputPublished?.Invoke(output);
        return true;
    }

    public IReadOnlyList<GameSessionSnapshot> ListSessions()
    {
        lock (_gate)
            return _sessions.Values.Select(static record => record.Snapshot).OrderByDescending(static session => session.StartedAt).ToArray();
    }

    public bool TryGetSession(Guid sessionId, out GameSessionSnapshot? session)
    {
        lock (_gate)
        {
            if (_sessions.TryGetValue(sessionId, out SessionRecord? record))
            {
                session = record.Snapshot;
                return true;
            }
        }
        session = null;
        return false;
    }

    public IReadOnlyList<GameProcessOutput> ReadOutput(Guid sessionId, long afterSequence, int maximumCount = 256)
    {
        if (maximumCount is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        lock (_gate)
            return _sessions.TryGetValue(sessionId, out SessionRecord? record)
                ? record.Output.Where(item => item.Sequence > afterSequence).Take(maximumCount).ToArray()
                : [];
    }

    private long NextSequence() => checked(++_sequence);
    private static bool IsTerminal(GameSessionState state) => state is GameSessionState.Exited or GameSessionState.Crashed or GameSessionState.Terminated;
    private void Publish(GameLaunchEvent launchEvent) => LaunchEventPublished?.Invoke(launchEvent);

    private sealed class SessionRecord(GameSessionSnapshot snapshot)
    {
        public GameSessionSnapshot Snapshot { get; set; } = snapshot;
        public Queue<GameProcessOutput> Output { get; } = new();
    }
}
