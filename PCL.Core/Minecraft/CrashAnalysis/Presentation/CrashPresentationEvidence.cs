namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashPresentationEvidence
{
    public required string TitleKey { get; init; }
    public required CrashFactKind FactKind { get; init; }
    public CrashLogKind SourceKind { get; init; }
    public string? SourceName { get; init; }
    public int? LineNumber { get; init; }
    public string? Excerpt { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public int Weight { get; init; }
}