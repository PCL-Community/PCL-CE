namespace PCL;

internal sealed class CrashAnalysisResult
{
    private readonly List<CrashFinding> _findings = [];

    public IReadOnlyList<CrashFinding> Findings => _findings;

    public bool HasFinding => _findings.Count > 0;

    public bool Any => HasFinding;

    public void Add(CrashFinding finding)
    {
        var existing = _findings.FirstOrDefault(item => item.Cause == finding.Cause);
        if (existing is null)
        {
            _findings.Add(finding);
            return;
        }

        foreach (var evidence in finding.Evidence)
            existing.AddEvidence(evidence);

        existing.ShouldStop |= finding.ShouldStop;
    }

    public void Add(
        CrashCause cause,
        CrashConfidence confidence,
        IEnumerable<string>? details = null,
        CrashLogKind? source = null,
        string? pattern = null,
        string? displayKind = null,
        bool shouldStop = false)
    {
        Add(new CrashFinding(cause, confidence, ToEvidence(details, source, pattern, displayKind))
        {
            ShouldStop = shouldStop
        });
    }

    private static IEnumerable<CrashEvidence> ToEvidence(
        IEnumerable<string>? details,
        CrashLogKind? source,
        string? pattern,
        string? displayKind)
    {
        if (details is null)
            yield break;

        foreach (var detail in details)
        {
            if (string.IsNullOrWhiteSpace(detail))
                continue;

            yield return new CrashEvidence
            {
                Value = detail.Trim(),
                Source = source,
                Pattern = pattern,
                DisplayKind = displayKind
            };
        }
    }
}