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
        new SystemInfoParser()
    ];

    public CrashFactSet Extract(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();
        foreach (var parser in _parsers)
            facts.AddRange(parser.Parse(bundle, request));

        return CrashFactSetNormalizer.Normalize(facts);
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