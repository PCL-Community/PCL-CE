namespace PCL.Core.Minecraft.CrashAnalysis;

internal sealed partial class ModMetadataParser : ICrashLogParser
{
    private static readonly HashSet<string> _IgnoredModIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "resolution", "loading", "loader", "version", "dependency", "dependencies", "mod", "mods"
    };

    public IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();

        foreach (var document in bundle.Documents)
            _AppendDocumentFacts(facts, document);

        return facts;
    }

    private static void _AppendDocumentFacts(List<CrashFact> facts, CrashLogDocument document)
    {
        var lines = CrashText.ReadLines(document.Text);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AppendModIdFacts(facts, document, line, lineNumber);
            _AppendModFileFact(facts, document, line, lineNumber);
            _AppendConfigFact(facts, document, line, lineNumber);
        }
    }

    private static void _AppendModIdFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        facts.AddRange(_ExtractExplicitModIds(line).Select(id =>
            CrashFactFactory.Create(
                CrashFactKind.ModCandidateDetected,
                id, document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.Medium,
                visibility: CrashFactVisibility.Technical)));
    }

    private static void _AppendModFileFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (!_ModFileIssueRegex().IsMatch(line))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.ModFileCorrupted,
            line.Trim(),
            document,
            line,
            lineNumber,
            confidence: CrashFactConfidence.Medium));
    }

    private static void _AppendConfigFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (!_ConfigParseRegex().IsMatch(line))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.ConfigParseIssueDetected,
            line.Trim(),
            document,
            line,
            lineNumber,
            confidence: CrashFactConfidence.Medium));
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.ModConfigParseFailed,
            line.Trim(),
            document,
            line,
            lineNumber,
            confidence: CrashFactConfidence.Medium));
    }

    private static IEnumerable<string> _ExtractExplicitModIds(string line)
    {
        var explicitId = _ExplicitModIdRegex().Match(line);

        if (_TryGetAllowedModId(explicitId, out var id))
            yield return id;

        var fabricDisplay = _FabricModDisplayRegex().Match(line);

        if (_TryGetAllowedModId(fabricDisplay, out id))
            yield return id;
    }

    private static bool _TryGetAllowedModId(Match match, out string id)
    {
        id = match.Success ? match.Groups["id"].Value.Trim() : string.Empty;
        return _IsAllowedModId(id);
    }

    private static bool _IsAllowedModId(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && !_IgnoredModIds.Contains(id.Trim());
    }

    private static bool _Contains(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?i)\b(?:modid|mod id)\s*[:=]\s*(?<id>[a-z0-9_.-]{2,})\b")]
    private static partial Regex _ExplicitModIdRegex();

    [GeneratedRegex(@"(?i)Mod\s+'(?<display>[^']+)'\s+\((?<id>[a-z0-9_.-]+)\)")]
    private static partial Regex _FabricModDisplayRegex();

    [GeneratedRegex(
        @"(?i)failed to load.*\.jar|invalid mod file|failed to load mod file|zip END header not found|invalid CEN header|error in opening zip file|unable to read mod metadata|no mods\.toml found|invalid fabric\.mod\.json|fabric\.mod\.json.*(?:missing|invalid)")]
    private static partial Regex _ModFileIssueRegex();

    [GeneratedRegex(
        @"(?i)(?:config|\.toml|\.json|nightconfig|JsonSyntaxException|ParsingException).*?(?:parse|parsing|invalid|malformed|failed|error)|(?:parse|parsing|invalid|malformed|failed|error).*?(?:config|\.toml|\.json|nightconfig)")]
    private static partial Regex _ConfigParseRegex();
}