namespace PCL.Core.Minecraft.CrashAnalysis;

internal sealed partial class MinecraftCrashReportParser : ICrashLogParser
{
    public IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();

        foreach (var document in bundle.Documents)
        {
            if (!_ShouldParseDocument(document))
                continue;

            _AppendDocumentFacts(facts, document);
        }

        _AppendRuntimeFacts(facts, request);

        return facts;
    }

    private static bool _ShouldParseDocument(CrashLogDocument document)
    {
        return document.Kind is CrashLogKind.MinecraftCrashReport
            or CrashLogKind.MinecraftLatestLog
            or CrashLogKind.CapturedGameOutput;
    }

    private static void _AppendDocumentFacts(List<CrashFact> facts, CrashLogDocument document)
    {
        if (document.Kind == CrashLogKind.MinecraftCrashReport)
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.MinecraftCrashReportPresent,
                document.Name,
                document));

        var lines = CrashText.ReadLines(document.Text);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AppendMinecraftVersionFact(facts, document, line, lineNumber);
            _AppendMainExceptionFact(facts, document, line, lineNumber);
            _AppendWorldIssueFacts(facts, document, line, lineNumber);
            _AppendClientContentIssueFacts(facts, document, line, lineNumber);
        }
    }

    private static void _AppendRuntimeFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        var minecraftVersion = request.RuntimeContext.MinecraftVersion;

        if (string.IsNullOrWhiteSpace(minecraftVersion))
            return;

        facts.Add(CrashFactFactory.CreateFromContext(
            CrashFactKind.MinecraftVersionDetected,
            minecraftVersion));
    }

    private static void _AppendMinecraftVersionFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var match = _MinecraftVersionRegex().Match(line);

        if (!match.Success)
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.MinecraftVersionDetected,
            match.Groups["version"].Value,
            document,
            line,
            lineNumber));
    }

    private static void _AppendMainExceptionFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var match = _ExceptionLineRegex().Match(line);

        if (!match.Success)
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.MinecraftMainException,
            match.Groups["type"].Value,
            document,
            line,
            lineNumber));
    }

    private static void _AppendWorldIssueFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_Contains(line, "Ticking entity") || _Contains(line, "Entity being ticked"))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.WorldEntityIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber));

        if (_Contains(line, "Block entity being ticked") || _Contains(line, "Block location"))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.WorldBlockEntityIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber));
    }

    private static void _AppendClientContentIssueFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_Contains(line, "resource pack"))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ResourcePackIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.Medium));

        if (_Contains(line, "shader") || _Contains(line, "OpenGL error 1282"))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ShaderIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.Medium));
    }

    private static bool _Contains(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?i)(?:minecraft version|version:)\s*(?<version>\d+\.\d+(?:\.\d+)?)")]
    private static partial Regex _MinecraftVersionRegex();

    [GeneratedRegex(@"(?<type>[a-zA-Z_][\w\.]+(?:Exception|Error))(?::|$)")]
    private static partial Regex _ExceptionLineRegex();
}