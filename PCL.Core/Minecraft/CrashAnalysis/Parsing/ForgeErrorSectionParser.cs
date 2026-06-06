namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     解析 Forge / NeoForge 的 Mod loading error 区域。相比普通逐行扫描，段落解析可以
///     把同一个加载错误合并为一条事实，并提取受影响 Mod、缺失依赖和版本要求。
/// </summary>
internal sealed partial class ForgeErrorSectionParser : ICrashLogParser
{
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
            if (!_LooksLikeForgeErrorStart(line))
                continue;

            var block = _ReadForgeBlock(lines, index);
            var properties = _ExtractProperties(block);
            var loadingLine = _FindBestLine(lines, index, _ForgeErrorStartRegex()) ?? (line, index + 1);
            var dependencyLine = _FindBestLine(lines, index, _NeoForgeDependencyLineRegex()) ??
                                 _FindBestLine(lines, index, _ForgeRequiresLineRegex()) ??
                                 _FindBestLine(lines, index, _MandatoryDependencyRegex());

            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ForgeModLoadingErrorDetected,
                _Trim(loadingLine.Line),
                document,
                loadingLine.Line,
                loadingLine.LineNumber,
                properties,
                visibility: CrashFactVisibility.Main));
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.LoaderModLoadingFailed,
                _Trim(loadingLine.Line),
                document,
                loadingLine.Line,
                loadingLine.LineNumber,
                properties,
                visibility: CrashFactVisibility.Main));

            if (_MandatoryDependencyRegex().IsMatch(block) || dependencyLine is not null)
            {
                var evidenceLine = dependencyLine ?? loadingLine;
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.ForgeMissingMandatoryDependencyDetected,
                    _DependencySummary(properties, evidenceLine.Line),
                    document,
                    block,
                    evidenceLine.LineNumber,
                    properties,
                    visibility: CrashFactVisibility.Main));
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.MissingModDependencyDetected,
                    _DependencySummary(properties, evidenceLine.Line),
                    document,
                    block,
                    evidenceLine.LineNumber,
                    properties,
                    visibility: CrashFactVisibility.Main));
            }

            if (_LanguageProviderRegex().IsMatch(block))
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.ForgeLanguageProviderMissingDetected,
                    _Trim(_FindBestLine(lines, index, _LanguageProviderRegex())?.Line ?? loadingLine.Line),
                    document,
                    block,
                    (_FindBestLine(lines, index, _LanguageProviderRegex()) ?? loadingLine).LineNumber,
                    properties,
                    visibility: CrashFactVisibility.Main));

            if (_VersionRequirementRegex().IsMatch(block))
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.LoaderVersionRequirementDetected,
                    _Trim(_FindBestLine(lines, index, _VersionRequirementRegex())?.Line ?? loadingLine.Line),
                    document,
                    block,
                    (_FindBestLine(lines, index, _VersionRequirementRegex()) ?? loadingLine).LineNumber,
                    properties,
                    visibility: CrashFactVisibility.Main));
        }
    }

    private static (string Line, int LineNumber)? _FindBestLine(
        IReadOnlyList<string> lines,
        int startIndex,
        Regex regex)
    {
        for (var index = startIndex; index < Math.Min(lines.Count, startIndex + 24); index++)
        {
            var line = lines[index];
            if (regex.IsMatch(line))
                return (line.Trim(), index + 1);
        }

        return null;
    }

    private static bool _LooksLikeForgeErrorStart(string line)
    {
        return _ForgeErrorStartRegex().IsMatch(line) ||
               _MandatoryDependencyRegex().IsMatch(line) ||
               _LanguageProviderRegex().IsMatch(line);
    }

    private static string _ReadForgeBlock(IReadOnlyList<string> lines, int startIndex)
    {
        var start = Math.Max(0, startIndex - 2);
        var end = startIndex;
        for (var index = startIndex; index < Math.Min(lines.Count, startIndex + 24); index++)
        {
            var line = lines[index];
            if (index > startIndex && _HardStopRegex().IsMatch(line))
                break;
            end = index;
        }

        return string.Join("\n", lines.Skip(start).Take(end - start + 1));
    }

    private static IReadOnlyDictionary<string, string> _ExtractProperties(string block)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LoaderName"] = block.Contains("neoforge", StringComparison.OrdinalIgnoreCase) ? "NeoForge" : "Forge"
        };

        var neoforgeDependency = _NeoForgeDependencyLineRegex().Match(block);
        if (neoforgeDependency.Success)
        {
            properties.TryAdd("MissingModId", neoforgeDependency.Groups["missing"].Value.Trim());
            properties.TryAdd("AffectedModId", neoforgeDependency.Groups["affected"].Value.Trim());
            properties.TryAdd("RequiredVersion", neoforgeDependency.Groups["version"].Value.Trim());
            properties.TryAdd("CurrentVersion", _NormalizeCurrentVersion(neoforgeDependency.Groups["current"].Value));
        }

        var forgeRequires = _ForgeRequiresLineRegex().Match(block);
        if (forgeRequires.Success)
        {
            properties.TryAdd("AffectedModId", forgeRequires.Groups["affected"].Value.Trim());
            properties.TryAdd("MissingModId", forgeRequires.Groups["missing"].Value.Trim());
            properties.TryAdd("RequiredVersion", forgeRequires.Groups["version"].Value.Trim());
        }

        var quoted = _QuotedModRegex().Match(block);
        if (quoted.Success)
        {
            properties.TryAdd("AffectedMod", quoted.Groups["display"].Value.Trim());
            if (quoted.Groups["id"].Success)
                properties.TryAdd("AffectedModId", quoted.Groups["id"].Value.Trim());
        }

        var explicitId = _ExplicitModIdRegex().Match(block);
        if (explicitId.Success)
            properties.TryAdd("AffectedModId", explicitId.Groups["id"].Value.Trim());

        var missing = _MissingDependencyRegex().Match(block);
        if (missing.Success)
        {
            properties.TryAdd("MissingModId", missing.Groups["missing"].Value.Trim());
            if (missing.Groups["version"].Success)
                properties.TryAdd("RequiredVersion", missing.Groups["version"].Value.Trim());
        }

        var version = _VersionRangeRegex().Match(block);
        if (version.Success)
            properties.TryAdd("RequiredVersion", version.Groups["version"].Value.Trim());

        if (properties.TryGetValue("AffectedModId", out var affectedModId))
            properties.TryAdd("AffectedMod", affectedModId);

        return properties;
    }

    private static string _DependencySummary(
        IReadOnlyDictionary<string, string> properties,
        string fallbackLine)
    {
        if (properties.TryGetValue("AffectedMod", out var affected) &&
            properties.TryGetValue("MissingModId", out var missing) &&
            properties.TryGetValue("RequiredVersion", out var version))
            return affected + " requires " + missing + " " + version + ", but it is missing.";

        if (properties.TryGetValue("AffectedModId", out var affectedId) &&
            properties.TryGetValue("MissingModId", out var missingId))
            return affectedId + " requires " + missingId + ", but it is missing.";

        return _Trim(fallbackLine);
    }

    private static string _NormalizeCurrentVersion(string value)
    {
        value = value.Trim().Trim('[', ']');
        return value.Equals("MISSING", StringComparison.OrdinalIgnoreCase) ? "missing" : value;
    }

    private static string _Trim(string value)
    {
        value = value.Trim();
        return value.Length > 220 ? value[..220] + "..." : value;
    }

    private static string _Summary(string value)
    {
        var line = CrashText.ReadLines(value)
            .Select(static item => item.Trim())
            .FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item) &&
                                           !item.StartsWith("at ", StringComparison.OrdinalIgnoreCase));
        line ??= value.Trim();
        return line.Length > 220 ? line[..220] + "..." : line;
    }

    [GeneratedRegex(
        @"(?i)Mod ID:\s*'(?<missing>[a-z0-9_.-]+)'\s*,\s*Requested by:\s*'(?<affected>[a-z0-9_.-]+)'\s*,\s*Expected range:\s*'(?<version>[^']+)'\s*,\s*Actual version:\s*'(?<current>[^']+)'")]
    private static partial Regex _NeoForgeDependencyLineRegex();

    [GeneratedRegex(
        @"(?i)Mod\s+(?<affected>[a-z0-9_.-]+)\s+requires\s+(?<missing>[a-z0-9_.-]+)\s+(?<version>.+?)(?:$|Currently|Reason)")]
    private static partial Regex _ForgeRequiresLineRegex();

    [GeneratedRegex(
        @"(?i)Mod loading error has occurred|ModLoadingException|error loading mods|failed to load mod file")]
    private static partial Regex _ForgeErrorStartRegex();

    [GeneratedRegex(
        @"(?i)Missing(?: or unsupported)? mandatory dependencies|missing mandatory dependency|requires.*(?:forge|minecraft|neoforge).*(?:missing|not found)")]
    private static partial Regex _MandatoryDependencyRegex();

    [GeneratedRegex(
        @"(?i)needs language provider|missing language provider|language provider\s+(?:javafml|fmlcore|fmlmod)")]
    private static partial Regex _LanguageProviderRegex();

    [GeneratedRegex(
        @"(?i)requires\s+(?:minecraft|forge|neoforge)\s*(?:version)?|wrong\s+(?:minecraft|loader)\s+version|incompatible.*(?:minecraft|forge|neoforge|loader)|needs.*(?:minecraft|forge|neoforge).*(?:version|\[)")]
    private static partial Regex _VersionRequirementRegex();

    [GeneratedRegex("""(?i)Mod(?: File)?\s+[\'"](?<display>[^\'"]+)[\'"]\s*(?:\((?<id>[a-z0-9_.-]+)\))?""")]
    private static partial Regex _QuotedModRegex();

    [GeneratedRegex(@"(?i)\b(?:modid|mod id)\s*[:=]\s*(?<id>[a-z0-9_.-]{2,})\b")]
    private static partial Regex _ExplicitModIdRegex();

    [GeneratedRegex(
        """(?i)(?:missing|required|dependency)\s+(?:mod\s+)?[\'"]?(?<missing>[a-z0-9_.-]{2,})[\'"]?(?:\s+(?<version>\[[^\]]+\]|\([^\)]+\)|\d[^\s,;]+))?""")]
    private static partial Regex _MissingDependencyRegex();

    [GeneratedRegex(@"(?<version>[\[\(]\d[^\]\)]+[\]\)])")]
    private static partial Regex _VersionRangeRegex();

    [GeneratedRegex(@"^\s*(?:at\s|Caused by:|Exception in thread|--\s|\[\d{2}:\d{2}:\d{2})")]
    private static partial Regex _HardStopRegex();
}