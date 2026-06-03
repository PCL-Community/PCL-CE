namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashPresentationLogSource
{
    public required CrashLogKind Kind { get; init; }
    public required string Name { get; init; }
    public string? FullPath { get; init; }
    public long? Length { get; init; }
    public bool UsedForAnalysis { get; init; }
    public string Preview { get; init; } = string.Empty;
}

public sealed record CrashPresentationEnvironmentItem
{
    public required string GroupKey { get; init; }
    public required string NameKey { get; init; }
    public required string Value { get; init; }
    public bool IsSensitive { get; init; }
}