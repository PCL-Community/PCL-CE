namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     解析 Minecraft crash report 的结构化段落。这个解析器只读取 crash report
///     中的标题、段落和系统详情，不再把整份报告当作普通逐行日志扫描。
/// </summary>
internal sealed partial class CrashReportSectionParser : ICrashLogParser
{
    public IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();

        foreach (var document in bundle.Documents
                     .Where(static item => item.Kind == CrashLogKind.MinecraftCrashReport))
            _AppendDocumentFacts(facts, document);

        return facts;
    }

    private static void _AppendDocumentFacts(List<CrashFact> facts, CrashLogDocument document)
    {
        var lines = CrashText.ReadLines(document.Text);
        if (lines.Count == 0) return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.MinecraftCrashReportPresent,
            document.Name,
            document,
            lineNumber: 1,
            visibility: CrashFactVisibility.Technical));

        _AppendDescriptionFact(facts, document, lines);
        _AppendTopExceptionFact(facts, document, lines);
        _AppendStackRootFact(facts, document, lines);
        _AppendStructuredSectionFacts(facts, document, lines);
        _AppendSystemDetailsFacts(facts, document, lines);
    }

    private static void _AppendDescriptionFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var match = _DescriptionRegex().Match(lines[index]);
            if (!match.Success) continue;

            var description = match.Groups["value"].Value.Trim();
            if (string.IsNullOrWhiteSpace(description)) return;

            facts.Add(CrashFactFactory.Create(
                CrashFactKind.CrashReportDescriptionDetected,
                description,
                document,
                lines[index],
                index + 1,
                visibility: CrashFactVisibility.Main));

            if (_ManualDebugCrashRegex().IsMatch(description))
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.ManualDebugCrashDetected,
                    description,
                    document,
                    lines[index],
                    index + 1,
                    visibility: CrashFactVisibility.Main,
                    strength: CrashFactStrength.Direct,
                    scope: CrashFactScope.RootCause));
            return;
        }
    }

    private static void _AppendTopExceptionFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = _TopExceptionRegex().Match(line);
            if (!match.Success) continue;

            var exception = match.Groups["type"].Value.Trim();
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.CrashReportTopExceptionDetected,
                exception,
                document,
                line,
                index + 1,
                visibility: CrashFactVisibility.Main));
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.MinecraftMainException,
                exception,
                document,
                line,
                index + 1,
                confidence: CrashFactConfidence.High,
                visibility: CrashFactVisibility.Main));

            if (_ManualDebugCrashRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.ManualDebugCrashDetected,
                    line,
                    document,
                    line,
                    index + 1,
                    visibility: CrashFactVisibility.Main,
                    strength: CrashFactStrength.Direct,
                    scope: CrashFactScope.RootCause));
            return;
        }
    }

    private static void _AppendStackRootFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        IReadOnlyList<string> lines)
    {
        var stackIndex = _FindLine(lines,
            static line => line.Trim().Equals("Stacktrace:", StringComparison.OrdinalIgnoreCase));
        if (stackIndex < 0) return;

        for (var index = stackIndex + 1; index < Math.Min(lines.Count, stackIndex + 18); index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.StartsWith("-- ", StringComparison.Ordinal))
                return;

            facts.Add(CrashFactFactory.Create(
                CrashFactKind.CrashReportStackRootDetected,
                line,
                document,
                line,
                index + 1,
                confidence: CrashFactConfidence.Medium,
                visibility: CrashFactVisibility.Main));
            return;
        }
    }

    private static void _AppendStructuredSectionFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        IReadOnlyList<string> lines)
    {
        foreach (var section in _ReadSections(lines))
        {
            if (section.Name.Contains("suspected mods", StringComparison.OrdinalIgnoreCase))
                _AppendSuspectedMods(facts, document, section);
            if (section.Name.Contains("entity being ticked", StringComparison.OrdinalIgnoreCase))
                _AppendEntitySection(facts, document, section);
            if (section.Name.Contains("block entity being ticked", StringComparison.OrdinalIgnoreCase) ||
                section.Name.Contains("ticking block entity", StringComparison.OrdinalIgnoreCase))
                _AppendBlockEntitySection(facts, document, section);
        }
    }

    private static void _AppendSuspectedMods(
        List<CrashFact> facts,
        CrashLogDocument document,
        CrashReportSection section)
    {
        var content = _Summary(section.Lines);
        if (string.IsNullOrWhiteSpace(content)) return;

        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var match = _SuspectedModIdRegex().Match(content);
        if (match.Success)
            properties["ModId"] = match.Groups["id"].Value.Trim();

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.CrashReportSuspectedModDetected,
            content,
            document,
            string.Join("\n", section.Lines.Take(8)),
            section.StartLine,
            properties,
            visibility: CrashFactVisibility.Main));

        if (properties.TryGetValue("ModId", out var modId))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ModCandidateDetected,
                modId,
                document,
                string.Join("\n", section.Lines.Take(8)),
                section.StartLine,
                properties));
    }

    private static void _AppendEntitySection(
        List<CrashFact> facts,
        CrashLogDocument document,
        CrashReportSection section)
    {
        var value = _Summary(section.Lines);
        if (string.IsNullOrWhiteSpace(value)) return;

        var properties = _ExtractKeyValueProperties(section.Lines);
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.CrashReportEntityTickDetected,
            value,
            document,
            string.Join("\n", section.Lines.Take(10)),
            section.StartLine,
            properties,
            visibility: CrashFactVisibility.Main));
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.WorldEntityIssueDetected,
            value,
            document,
            string.Join("\n", section.Lines.Take(10)),
            section.StartLine,
            properties,
            visibility: CrashFactVisibility.Main));
    }

    private static void _AppendBlockEntitySection(
        List<CrashFact> facts,
        CrashLogDocument document,
        CrashReportSection section)
    {
        var value = _Summary(section.Lines);
        if (string.IsNullOrWhiteSpace(value)) return;

        var properties = _ExtractKeyValueProperties(section.Lines);
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.CrashReportBlockEntityTickDetected,
            value,
            document,
            string.Join("\n", section.Lines.Take(10)),
            section.StartLine,
            properties,
            visibility: CrashFactVisibility.Main));
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.WorldBlockEntityIssueDetected,
            value,
            document,
            string.Join("\n", section.Lines.Take(10)),
            section.StartLine,
            properties,
            visibility: CrashFactVisibility.Main));
    }

    private static void _AppendSystemDetailsFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        IReadOnlyList<string> lines)
    {
        var system = _ReadSections(lines)
            .FirstOrDefault(static item =>
                item.Name.Contains("system details", StringComparison.OrdinalIgnoreCase));
        if (system is null) return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.CrashReportSystemDetailsDetected,
            _Summary(system.Lines),
            document,
            string.Join("\n", system.Lines.Take(12)),
            system.StartLine,
            visibility: CrashFactVisibility.Technical));

        foreach (var item in system.Lines)
        {
            var line = item.Trim();
            var lineNumber = system.StartLine + system.Lines.IndexOf(item);
            _AppendSystemVersionFact(facts, document, line, lineNumber);
        }
    }

    private static void _AppendSystemVersionFact(
        List<CrashFact> facts,
        CrashLogDocument document, string line,
        int lineNumber)
    {
        var minecraft = _MinecraftVersionRegex().Match(line);
        if (minecraft.Success)
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.MinecraftVersionDetected,
                minecraft.Groups["version"].Value,
                document,
                line,
                lineNumber,
                visibility: CrashFactVisibility.Technical));

        var java = _JavaVersionRegex().Match(line);
        if (java.Success)
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.JavaVersionDetected,
                java.Groups["version"].Value,
                document,
                line,
                lineNumber,
                _JavaProperties(java.Groups["version"].Value),
                visibility: CrashFactVisibility.Technical));
    }

    private static IReadOnlyDictionary<string, string> _JavaProperties(string value)
    {
        var result = new Dictionary<string, string>();
        var match = _JavaMajorRegex().Match(value);
        if (match.Success && int.TryParse(match.Groups["major"].Value, out var major))
            result["JavaMajor"] = major.ToString();
        return result;
    }

    private static IReadOnlyDictionary<string, string> _ExtractKeyValueProperties(IReadOnlyList<string> lines)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var match = _KeyValueRegex().Match(line);
            if (!match.Success) continue;
            var key = match.Groups["key"].Value.Trim();
            var value = match.Groups["value"].Value.Trim();
            if (!properties.ContainsKey(key) && !string.IsNullOrWhiteSpace(value))
                properties[key] = value;
        }

        return properties;
    }

    private static IReadOnlyList<CrashReportSection> _ReadSections(IReadOnlyList<string> lines)
    {
        var sections = new List<CrashReportSection>();
        CrashReportSection? current = null;

        for (var index = 0; index < lines.Count; index++)
        {
            var match = _SectionHeaderRegex().Match(lines[index]);
            if (match.Success)
            {
                if (current is not null) sections.Add(current);
                current = new CrashReportSection(match.Groups["name"].Value.Trim(), index + 1, []);
                continue;
            }

            current?.Lines.Add(lines[index]);
        }

        if (current is not null) sections.Add(current);
        return sections;
    }

    private static int _FindLine(IReadOnlyList<string> lines, Func<string, bool> predicate)
    {
        for (var index = 0; index < lines.Count; index++)
            if (predicate(lines[index]))
                return index;
        return -1;
    }

    private static string _Summary(IReadOnlyList<string> lines)
    {
        var value = lines
            .Select(static line => line.Trim())
            .FirstOrDefault(static line =>
                !string.IsNullOrWhiteSpace(line) && !line.StartsWith("at ", StringComparison.OrdinalIgnoreCase));
        value ??= string.Join(" ",
                lines.Select(static line => line.Trim())
                    .Where(static line => !string.IsNullOrWhiteSpace(line)))
            .Trim();
        return value.Length > 220 ? value[..220] + "..." : value;
    }

    [GeneratedRegex(@"(?i)Manually triggered debug crash|F3\s*\+\s*C")]
    private static partial Regex _ManualDebugCrashRegex();

    [GeneratedRegex(@"(?i)^\s*Description:\s*(?<value>.+)\s*$")]
    private static partial Regex _DescriptionRegex();

    [GeneratedRegex(@"(?i)^(?:Caused by:\s*)?(?<type>[a-z0-9_.$]+(?:Exception|Error|Throwable))(?:[:\s]|$)")]
    private static partial Regex _TopExceptionRegex();

    [GeneratedRegex(@"^--\s*(?<name>[^-]+?)\s*--\s*$")]
    private static partial Regex _SectionHeaderRegex();

    [GeneratedRegex(@"(?i)\b(?:modid|mod id|id)\s*[:=]\s*(?<id>[a-z0-9_.-]{2,})\b")]
    private static partial Regex _SuspectedModIdRegex();

    [GeneratedRegex(@"^\s*(?<key>[A-Za-z0-9 _/.-]{2,40})\s*:\s*(?<value>.+)\s*$")]
    private static partial Regex _KeyValueRegex();

    [GeneratedRegex(@"(?i)^\s*Minecraft\s+Version\s*:\s*(?<version>[0-9][0-9A-Za-z_.+-]*)")]
    private static partial Regex _MinecraftVersionRegex();

    [GeneratedRegex(@"(?i)Java(?:\sVersion| version)?\s*:\s*(?<version>\d+(?:\.\d+){0,3})")]
    private static partial Regex _JavaVersionRegex();

    [GeneratedRegex(@"(?<major>\d+)(?:\.\d+)*")]
    private static partial Regex _JavaMajorRegex();

    private sealed record CrashReportSection(string Name, int StartLine, List<string> Lines);
}