namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashAnalysisResult
{
    public required DateTimeOffset CreatedAt { get; init; }
    public TimeSpan AnalysisDuration { get; init; }
    public required CrashAnalysisRequest Request { get; init; }
    public required CrashLogBundle LogBundle { get; init; }
    public required CrashFactSet Facts { get; init; }
    public IReadOnlyList<CrashDiagnosis> Diagnoses { get; init; } = [];
    public required CrashPresentationModel Presentation { get; init; }

    public int FactCount => Facts.Facts.Count;

    public CrashDiagnosis? TopDiagnosis => Diagnoses.Count == 0 ? null : Diagnoses[0];
}