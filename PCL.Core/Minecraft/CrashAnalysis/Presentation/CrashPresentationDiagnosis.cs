namespace PCL.Core.Minecraft.CrashAnalysis;

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