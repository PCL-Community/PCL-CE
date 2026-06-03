namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashMarkdownDocument
{
    public required string FileName { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
}

public sealed record CrashMarkdownExportOptions
{
    public bool IncludeEvidence { get; init; } = true;
    public bool IncludeEnvironment { get; init; } = true;
    public bool IncludeLogs { get; init; } = true;
}

public delegate string CrashMarkdownLocalizer(string key, IReadOnlyDictionary<string, string> parameters);