namespace PCL.Core.Minecraft.CrashAnalysis;

internal static class CrashFactSetNormalizer
{
    public static CrashFactSet Normalize(IEnumerable<CrashFact> facts)
    {
        var result = (from @group in facts.GroupBy(_GetStableKey)
            let best = @group.OrderBy(static fact => fact.Visibility)
                .ThenByDescending(static fact => fact.Confidence)
                .ThenBy(static fact => fact.Value.Length)
                .First()
            select best with
            {
                Evidence = @group.SelectMany(static fact => fact.Evidence)
                    .GroupBy(static evidence => new
                        {
                            evidence.SourceKind, evidence.SourceName,
                            evidence.LineNumber, evidence.Excerpt
                        }
                    )
                    .Select(static item => item.First())
                    .Take(5)
                    .ToList()
            }).ToList();

        return new CrashFactSet { Facts = result };
    }

    private static string _GetStableKey(CrashFact fact)
    {
        var kind = fact.Kind.ToString();
        if (fact.Properties.TryGetValue("MissingModId", out var missing) && !string.IsNullOrWhiteSpace(missing))
            return kind + "|missing:" + missing.Trim().ToLowerInvariant() + "|" + _SourceKey(fact);
        if (fact.Properties.TryGetValue("AffectedModId", out var affected) &&
            fact.Kind is CrashFactKind.LoaderResolutionError or CrashFactKind.ModVersionConflictDetected)
            return kind + "|affected:" + affected.Trim().ToLowerInvariant() + "|" + _SourceKey(fact);

        return kind + "|" + CrashText.NormalizeEvidence(fact.Value) + "|" + _SourceKey(fact);
    }

    private static string _SourceKey(CrashFact fact)
    {
        var source = fact.Evidence.FirstOrDefault();
        return source?.SourceKind.ToString() ?? "context";
    }
}