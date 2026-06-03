namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashPresentationMetric
{
    public required string TitleKey { get; init; }
    public required string Value { get; init; }
    public string? DescriptionKey { get; init; }
}