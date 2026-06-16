namespace PCL;

internal sealed class CrashFinding
{
    public CrashFinding(
        CrashCause cause,
        CrashConfidence confidence,
        IEnumerable<CrashEvidence>? evidence = null)
    {
        Cause = cause;
        Confidence = confidence;
        Evidence = evidence
            ?.Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .ToList() ?? [];
    }

    public CrashCause Cause { get; }

    public CrashConfidence Confidence { get; }

    public List<CrashEvidence> Evidence { get; }

    public bool ShouldStop { get; set; }

    public IReadOnlyList<string> Details => Evidence
        .Select(item => item.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal)
        .ToList();

    public void AddEvidence(CrashEvidence evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence.Value))
            return;

        if (!Evidence.Any(item => item.EqualsTo(evidence)))
            Evidence.Add(evidence);
    }
}