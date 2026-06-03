namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashDiagnosisEvidence
{
    public required string FactId { get; init; }
    public required CrashFactKind FactKind { get; init; }
    public required CrashLogKind SourceKind { get; init; }
    public string? SourceName { get; init; }
    public string? Excerpt { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public int? LineNumber { get; init; }
    public int Weight { get; init; }
}