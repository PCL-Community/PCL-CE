namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     崩溃输入读取的安全与性能限制。
/// </summary>
public sealed record CrashInputOptions
{
    public const long MaxSingleLogBytes = 32L * 1024L * 1024L;
    public const long MaxArchiveBytes = 128L * 1024L * 1024L;
    public const int MaxArchiveLogCount = 128;
    public const int MaxLiveCandidateCount = 64;
    public static readonly TimeSpan RecentLogWindow = TimeSpan.FromMinutes(3);
}