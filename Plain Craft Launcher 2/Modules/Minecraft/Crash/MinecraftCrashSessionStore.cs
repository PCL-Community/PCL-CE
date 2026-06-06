using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL;

public sealed record MinecraftCrashSession
{
    public required string Id { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public McInstance? Instance { get; init; }
    public required CrashAnalysisRequest Request { get; init; }
    public required CrashAnalysisResult Result { get; init; }
    public required CrashPresentationModel Presentation { get; init; }
    public required CrashMarkdownDocument Markdown { get; init; }
    public IReadOnlyList<string> ExtraReportFiles { get; init; } = [];
}

public static class MinecraftCrashSessionStore
{
    private static readonly Dictionary<string, MinecraftCrashSession> _sessions = new();
    public static string? CurrentSessionId { get; private set; }

    public static MinecraftCrashSession Current =>
        TryGetCurrent() ?? throw new InvalidOperationException("当前没有可用的崩溃分析会话。");

    public static event Action? SessionChanged;

    public static MinecraftCrashSession? TryGetCurrent()
    {
        return CurrentSessionId is not null && _sessions.TryGetValue(CurrentSessionId, out var session)
            ? session
            : null;
    }

    public static void SetCurrent(MinecraftCrashSession session)
    {
        _sessions[session.Id] = session;
        CurrentSessionId = session.Id;
        foreach (var old in _sessions.Values.OrderByDescending(static item => item.CreatedAt).Skip(5).ToList())
            _sessions.Remove(old.Id);
        SessionChanged?.Invoke();
    }
}