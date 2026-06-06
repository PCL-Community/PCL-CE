namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     从日志中抽取出的客观事实。事实不是最终结论，只为诊断评分提供依据。
/// </summary>
public sealed record CrashFact
{
    public required string Id { get; init; }
    public required CrashFactKind Kind { get; init; }
    public required string Value { get; init; }
    public CrashFactConfidence Confidence { get; init; } = CrashFactConfidence.High;
    public CrashFactStrength Strength { get; init; } = CrashFactStrength.Strong;
    public CrashFactScope Scope { get; init; } = CrashFactScope.RootCause;
    public CrashFactVisibility Visibility { get; init; } = CrashFactVisibility.Main;
    public IReadOnlyList<CrashFactEvidence> Evidence { get; init; } = [];
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}