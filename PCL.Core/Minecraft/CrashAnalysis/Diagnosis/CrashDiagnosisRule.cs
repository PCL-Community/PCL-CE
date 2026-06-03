namespace PCL.Core.Minecraft.CrashAnalysis;

public abstract class CrashDiagnosisRule
{
    public abstract string Id { get; }
    public abstract CrashDiagnosisCode Code { get; }
    public abstract CrashDiagnosisCategory Category { get; }
    public abstract CrashDiagnosis? Evaluate(CrashLogBundle bundle, CrashFactSet facts, CrashAnalysisRequest request);

    protected static CrashDiagnosisEvidence Evidence(CrashFact fact, int weight)
    {
        var first = fact.Evidence.FirstOrDefault();
        return new CrashDiagnosisEvidence
        {
            FactId = fact.Id,
            FactKind = fact.Kind,
            SourceKind = first?.SourceKind ?? CrashLogKind.Unknown,
            SourceName = first?.SourceName,
            Excerpt = first?.Excerpt,
            Summary = _CreateEvidenceSummary(fact),
            Detail = first?.Excerpt ?? fact.Value,
            LineNumber = first?.LineNumber,
            Weight = weight
        };
    }

    private static string _CreateEvidenceSummary(CrashFact fact)
    {
        if (fact.Kind == CrashFactKind.MissingModDependencyDetected)
        {
            if (fact.Properties.TryGetValue("AffectedMod", out var affected) &&
                fact.Properties.TryGetValue("MissingModId", out var missing) &&
                fact.Properties.TryGetValue("RequiredVersion", out var version))
                return affected + " requires " + missing + " " + version + ", but it is missing.";
            if (fact.Properties.TryGetValue("AffectedModId", out var affectedId) &&
                fact.Properties.TryGetValue("MissingModId", out var missingId))
                return affectedId + " requires " + missingId + ", but it is missing.";
        }

        return CrashText.SummarizeEvidence(fact.Value);
    }

    protected CrashDiagnosis Create(
        int score,
        IReadOnlyList<CrashDiagnosisEvidence> evidence,
        IReadOnlyDictionary<string, string>? parameters = null,
        IReadOnlyList<CrashPresentationActionKind>? actions = null,
        CrashDiagnosisSeverity severity = CrashDiagnosisSeverity.Error)
    {
        score = CrashScore.Clamp(score);
        return new CrashDiagnosis
        {
            RuleId = Id,
            Code = Code,
            Category = Category,
            Severity = severity,
            Score = score,
            Confidence = CrashScore.ToConfidence(score),
            Evidence = evidence,
            Parameters = parameters ?? new Dictionary<string, string>(),
            SuggestedActionKinds = actions ?? []
        };
    }
}