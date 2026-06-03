namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashFactSet
{
    public IReadOnlyList<CrashFact> Facts { get; init; } = [];

    public IEnumerable<CrashFact> Find(CrashFactKind kind)
    {
        return Facts.Where(fact => fact.Kind == kind);
    }

    public bool Has(CrashFactKind kind)
    {
        return Facts.Any(fact => fact.Kind == kind);
    }

    public CrashFact? First(CrashFactKind kind)
    {
        return Facts.FirstOrDefault(fact => fact.Kind == kind);
    }
}