namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashFactExtractor
{
    private readonly IReadOnlyList<ICrashLogParser> _parsers =
    [
        new JavaCrashParser(),
        new MinecraftCrashReportParser(),
        new LoaderLogParser(),
        new ModMetadataParser(),
        new NativeCrashParser(),
        new FileSystemCrashParser(),
        new SystemInfoParser()
    ];

    public CrashFactSet Extract(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();
        foreach (var parser in _parsers)
            facts.AddRange(parser.Parse(bundle, request));

        return _Normalize(facts);
    }

    private static CrashFactSet _Normalize(IEnumerable<CrashFact> facts)
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
            return kind + "|missing:" + missing.Trim().ToLowerInvariant() + "|" + SourceKey(fact);
        if (fact.Properties.TryGetValue("AffectedModId", out var affected) &&
            fact.Kind is CrashFactKind.LoaderResolutionError or CrashFactKind.ModVersionConflictDetected)
            return kind + "|affected:" + affected.Trim().ToLowerInvariant() + "|" + SourceKey(fact);

        return kind + "|" + CrashText.NormalizeEvidence(fact.Value) + "|" + SourceKey(fact);

        static string SourceKey(CrashFact fact)
        {
            var source = fact.Evidence.FirstOrDefault();
            return source?.SourceKind.ToString() ?? "context";
        }
    }
}

internal interface ICrashLogParser
{
    IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request);
}

internal static class CrashFactFactory
{
    public static CrashFact Create(
        CrashFactKind kind,
        string value,
        CrashLogDocument document,
        string? excerpt = null,
        int? lineNumber = null,
        IReadOnlyDictionary<string, string>? properties = null,
        CrashFactConfidence confidence = CrashFactConfidence.High,
        CrashFactVisibility visibility = CrashFactVisibility.Main)
    {
        return new CrashFact
        {
            Id = kind + ":" + value,
            Kind = kind,
            Value = value,
            Confidence = confidence,
            Visibility = visibility,
            Properties = properties ?? new Dictionary<string, string>(),
            Evidence =
            [
                new CrashFactEvidence
                {
                    SourceKind = document.Kind,
                    SourceName = document.Name,
                    Excerpt = excerpt,
                    LineNumber = lineNumber
                }
            ]
        };
    }

    public static CrashFact CreateFromContext(CrashFactKind kind, string value,
        IReadOnlyDictionary<string, string>? properties = null,
        CrashFactVisibility visibility = CrashFactVisibility.Technical)
    {
        return new CrashFact
        {
            Id = kind + ":" + value,
            Kind = kind,
            Value = value,
            Visibility = visibility,
            Properties = properties ?? new Dictionary<string, string>()
        };
    }
}