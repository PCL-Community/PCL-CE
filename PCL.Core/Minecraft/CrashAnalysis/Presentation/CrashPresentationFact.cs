namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashPresentationFact
{
    public required CrashFactKind Kind { get; init; }
    public required string TitleKey { get; init; }
    public required string Value { get; init; }
    public CrashLogKind SourceKind { get; init; }
    public string? SourceName { get; init; }
    public int? LineNumber { get; init; }
    public string? Excerpt { get; init; }
}