namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     单个崩溃相关日志文档。文档保持来源和类型，不再把所有日志提前拼成一个全局字符串。
/// </summary>
public sealed record CrashLogDocument
{
    public required CrashLogKind Kind { get; init; }
    public required string Name { get; init; }
    public string? FullPath { get; init; }
    public CrashLogOrigin Origin { get; init; }
    public CrashLogAnalysisRole AnalysisRole { get; init; } = CrashLogAnalysisRole.Supporting;
    public DateTimeOffset? LastWriteTime { get; init; }
    public long? OriginalLength { get; init; }
    public required string Text { get; init; }

    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}