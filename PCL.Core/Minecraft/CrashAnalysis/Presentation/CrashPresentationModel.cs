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