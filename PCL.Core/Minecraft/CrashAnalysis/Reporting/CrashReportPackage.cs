namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashReportPackage(IReadOnlyList<CrashReportEntry> Entries);

public sealed record CrashReportEntry
{
    public required string FileName { get; init; }
    public required byte[] Content { get; init; }
}

public sealed record CrashReportBuildOptions
{
    public CrashMarkdownDocument? Markdown { get; init; }
    public IReadOnlyList<string> SensitiveValues { get; init; } = [];
}