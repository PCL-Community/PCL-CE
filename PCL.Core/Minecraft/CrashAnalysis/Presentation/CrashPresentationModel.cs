namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashPresentationModel
{
    public required CrashPresentationSummary Summary { get; init; }
    public IReadOnlyList<CrashPresentationDiagnosis> Diagnoses { get; init; } = [];
    public IReadOnlyList<CrashPresentationAction> Actions { get; init; } = [];
    public IReadOnlyList<CrashPresentationEvidence> Evidence { get; init; } = [];
    public IReadOnlyList<CrashPresentationFact> Facts { get; init; } = [];
    public IReadOnlyList<CrashPresentationMetric> Metrics { get; init; } = [];
    public IReadOnlyList<CrashPresentationLogSource> Logs { get; init; } = [];
    public IReadOnlyList<CrashPresentationEnvironmentItem> Environment { get; init; } = [];

    public bool HasUsefulDiagnosis => Diagnoses.Any(static diagnosis => diagnosis.Code != CrashDiagnosisCode.Unknown);
}

public sealed record CrashPresentationSummary
{
    public required CrashPresentationSeverity Severity { get; init; }
    public required string TitleKey { get; init; }
    public required string DescriptionKey { get; init; }
    public string? DetailKey { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

public enum CrashPresentationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record CrashPresentationDiagnosis
{
    public required CrashDiagnosisCode Code { get; init; }
    public required CrashDiagnosisCategory Category { get; init; }
    public required string TitleKey { get; init; }
    public required string DescriptionKey { get; init; }
    public required string CauseKey { get; init; }
    public required string ImpactKey { get; init; }
    public required string RecommendationKey { get; init; }
    public CrashDiagnosisConfidence Confidence { get; init; }
    public CrashDiagnosisSeverity Severity { get; init; }
    public int Score { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<CrashPresentationAction> Actions { get; init; } = [];
    public IReadOnlyList<CrashPresentationEvidence> Evidence { get; init; } = [];
    public IReadOnlyList<CrashDiagnosisNote> Notes { get; init; } = [];
}

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

public sealed record CrashPresentationMetric
{
    public required string TitleKey { get; init; }
    public required string Value { get; init; }
    public string? DescriptionKey { get; init; }
}

public sealed record CrashPresentationLogSource
{
    public required CrashLogKind Kind { get; init; }
    public required string Name { get; init; }
    public string? FullPath { get; init; }
    public long? Length { get; init; }
    public CrashLogAnalysisRole AnalysisRole { get; init; }
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