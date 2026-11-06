namespace PCL;

internal sealed class CrashRule
{
    public required CrashCause Cause { get; init; }

    public required DetectionPhase Phase { get; init; }

    public CrashConfidence Confidence { get; init; } = CrashConfidence.High;

    public bool StopOnMatch { get; init; }

    public required Func<CrashRuleInput, CrashFinding?> Evaluate { get; init; }
}

internal sealed class CrashRuleInput
{
    public required CrashLogSet Logs { get; init; }

    public required CrashModIndex ModIndex { get; init; }
}