namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashFactEvidence
{
    public required CrashLogKind SourceKind { get; init; }
    public string? SourceName { get; init; }
    public string? Excerpt { get; init; }
    public int? LineNumber { get; init; }
}