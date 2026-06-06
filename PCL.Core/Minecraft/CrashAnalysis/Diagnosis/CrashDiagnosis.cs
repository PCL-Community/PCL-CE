namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     诊断结果。诊断是若干事实经过评分后的候选结论，允许多个诊断同时存在。
/// </summary>
public sealed record CrashDiagnosis
{
    public required string RuleId { get; init; }
    public required CrashDiagnosisCode Code { get; init; }
    public required CrashDiagnosisCategory Category { get; init; }
    public CrashDiagnosisSeverity Severity { get; init; } = CrashDiagnosisSeverity.Error;
    public CrashDiagnosisNature Nature { get; init; } = CrashDiagnosisNature.ProbableCause;
    public CrashDiagnosisConfidence Confidence { get; init; }
    public int Score { get; init; }
    public IReadOnlyList<CrashDiagnosisEvidence> Evidence { get; init; } = [];
    public IReadOnlyList<CrashDiagnosisNote> Notes { get; init; } = [];
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<CrashPresentationActionKind> SuggestedActionKinds { get; init; } = [];
}