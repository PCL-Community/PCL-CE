namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashPresentationAction
{
    public required CrashPresentationActionKind Kind { get; init; }
    public required string TitleKey { get; init; }
    public string? DescriptionKey { get; init; }
    public CrashActionPriority Priority { get; init; }
    public CrashActionGroup Group { get; init; } = CrashActionGroup.Investigate;
    public int Order { get; init; }
    public string? TargetPath { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

public enum CrashPresentationActionKind
{
    OpenLog,
    ExportMarkdown,
    ExportReport,
    OpenJavaSettings,
    OpenMemorySettings,
    OpenInstanceModsFolder,
    OpenInstanceSettings,
    OpenResourcePackFolder,
    CopyDiagnosisSummary,
    PreviewMarkdown,
    ShowTechnicalDetails
}

public enum CrashActionPriority
{
    Primary,
    Secondary,
    More
}

public enum CrashActionGroup
{
    FixNow,
    Investigate,
    AskForHelp
}