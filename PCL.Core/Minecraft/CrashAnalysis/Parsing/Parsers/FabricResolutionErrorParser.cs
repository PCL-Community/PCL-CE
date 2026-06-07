namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     解析 Fabric / Quilt 的 resolution error 区域。它们通常包含缺失前置、版本要求和
///     Loader 给出的修复建议，逐行扫描容易把后续 Mixin 错误误认为首要原因。
/// </summary>
internal sealed partial class FabricResolutionErrorParser : ICrashLogParser
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
        var lines = document.Lines;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!_LooksLikeFabricResolutionLine(line))
                continue;

            var block = _ReadResolutionBlock(lines, index);
            var properties = _ExtractProperties(block.Text);
            var lineNumber = index + 1;

            facts.Add(CrashFactFactory.Create(
                CrashFactKind.LoaderResolutionError,
                _Summary(block.Text),
                document,
                block.Text,
                lineNumber,
                properties,
                visibility: CrashFactVisibility.Main));

            if (_IsMissingDependencyBlock(block.Text))
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.MissingModDependencyDetected,
                    _DependencySummary(block.Text, properties),
                    document,
                    block.Text,
                    _FindDependencyLineNumber(lines, block.StartIndex, block.EndIndex) ?? lineNumber,
                    properties,
                    visibility: CrashFactVisibility.Main));

            if (_FabricBreaksConflictRegex().IsMatch(block.Text))
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.ModSetConflictDetected,
                    _ConflictSummary(block.Text, properties),
                    document,
                    block.Text,
                    _FindConflictLineNumber(lines, block.StartIndex, block.EndIndex) ?? lineNumber,
                    properties,
                    visibility: CrashFactVisibility.Main));

            if (_FixRegex().IsMatch(block.Text))
                facts.Add(CrashFactFactory.Create(
                    CrashFactKind.LoaderProvidedSolutionDetected,
                    _Summary(_FixRegex().Match(block.Text).Value),
                    document,
                    block.Text,
                    _FindFixLineNumber(lines, block.StartIndex, block.EndIndex) ?? lineNumber,
                    properties,
                    CrashFactConfidence.Medium,
                    CrashFactVisibility.Technical));
        }
    }

    private static bool _LooksLikeFabricResolutionLine(string line)
    {
        return _ResolutionStartRegex().IsMatch(line) ||
               _FabricMissingDependencyRegex().IsMatch(line) ||
               _HardDependencyRegex().IsMatch(line);
    }

    private static ResolutionBlock _ReadResolutionBlock(IReadOnlyList<string> lines, int startIndex)
    {
        var start = Math.Max(0, startIndex - 3);
        var end = startIndex;
        for (var index = startIndex; index < Math.Min(lines.Count, startIndex + 18); index++)
        {
            var line = lines[index];
            if (index > startIndex && _HardStopRegex().IsMatch(line))
                break;
            end = index;
        }

        return new ResolutionBlock(
            string.Join("\n", lines.Skip(start).Take(end - start + 1)),
            start,
            end);
    }

    private static int? _FindDependencyLineNumber(IReadOnlyList<string> lines, int startIndex, int endIndex)
    {
        for (var index = startIndex; index <= endIndex && index < lines.Count; index++)
            if (_FabricMissingDependencyRegex().IsMatch(lines[index]) ||
                _HardDependencyRegex().IsMatch(lines[index]) ||
                _MissingDependencyRegex().IsMatch(lines[index]))
                return index + 1;

        return null;
    }

    private static int? _FindFixLineNumber(IReadOnlyList<string> lines, int startIndex, int endIndex)
    {
        for (var index = startIndex; index <= endIndex && index < lines.Count; index++)
            if (_FixRegex().IsMatch(lines[index]))
                return index + 1;

        return null;
    }

    private static int? _FindConflictLineNumber(
        IReadOnlyList<string> lines,
        int startIndex,
        int endIndex)
    {
        for (var index = startIndex; index <= endIndex && index < lines.Count; index++)
            if (_FabricBreaksConflictRegex().IsMatch(lines[index]))
                return index + 1;

        return null;
    }

    private static IReadOnlyDictionary<string, string> _ExtractProperties(string block)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var precise = _FabricMissingDependencyRegex().Match(block);
        if (precise.Success)
        {
            properties["AffectedMod"] = precise.Groups["display"].Value.Trim();
            properties["AffectedModId"] = precise.Groups["affected"].Value.Trim();
            properties["AffectedModVersion"] = precise.Groups["affectedVersion"].Value.Trim();
            properties["MissingModId"] = precise.Groups["missing"].Value.Trim();
            properties["RequiredVersion"] = _NormalizeRequirement(precise.Groups["requirement"].Value);
        }

        var hard = _HardDependencyRegex().Match(block);
        if (hard.Success)
        {
            properties.TryAdd("AffectedModId", hard.Groups["affected"].Value.Trim());
            properties.TryAdd("MissingModId", hard.Groups["missing"].Value.Trim());
            properties.TryAdd("RequiredVersion", hard.Groups["version"].Value.Trim());
        }

        var conflict = _FabricBreaksConflictRegex().Match(block);
        if (conflict.Success)
        {
            properties.TryAdd("ConflictModId", conflict.Groups["source"].Value.Trim());
            properties.TryAdd("ConflictingModId", conflict.Groups["target"].Value.Trim());
            properties.TryAdd("ConflictRange", conflict.Groups["range"].Value.Trim());
        }

        if (_IsMissingDependencyBlock(block))
        {
            var fix = _FixAddRegex().Match(block);
            if (fix.Success)
            {
                properties.TryAdd("MissingModId", fix.Groups["missing"].Value.Trim());
                properties.TryAdd("RequiredVersion", _NormalizeFabricFixVersion(fix.Groups["version"].Value));
            }
        }

        properties.TryAdd("LoaderName", "Fabric / Quilt");
        return properties;
    }

    private static string _DependencySummary(string block, IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("AffectedMod", out var affected) &&
            properties.TryGetValue("MissingModId", out var missing) &&
            properties.TryGetValue("RequiredVersion", out var version))
            return affected + " requires " + missing + " " + version + ", but it is missing.";

        if (properties.TryGetValue("AffectedModId", out var affectedId) &&
            properties.TryGetValue("MissingModId", out var missingId))
            return affectedId + " requires " + missingId + ", but it is missing.";

        return _Summary(block);
    }

    private static string _ConflictSummary(string block, IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("ConflictModId", out var source) &&
            properties.TryGetValue("ConflictingModId", out var target))
            return source + " breaks " + target + "; these mods cannot be loaded together.";

        return _Summary(block);
    }

    private static bool _IsMissingDependencyBlock(string block)
    {
        return _FabricMissingDependencyRegex().IsMatch(block) ||
               _HardDependencyRegex().IsMatch(block) ||
               _MissingDependencyRegex().IsMatch(block);
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

    private static string _NormalizeRequirement(string value)
    {
        value = value.Trim();
        value = value.Replace("any ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("version", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("versions", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
        return string.IsNullOrWhiteSpace(value) ? "compatible version" : value;
    }

    private static string _NormalizeFabricFixVersion(string value)
    {
        value = value.Trim();
        return value.EndsWith('-') ? value.TrimEnd('-') + ".x" : value;
    }

    [GeneratedRegex(@"(?i)Mod resolution failed|HARD_DEP_NO_CANDIDATE|could not resolve|failed to resolve")]
    private static partial Regex _ResolutionStartRegex();

    [GeneratedRegex(
        @"(?i)Mod\s+'(?<display>[^']+)'\s+\((?<affected>[a-z0-9_.-]+)\)\s+(?<affectedVersion>[^\s]+)\s+requires\s+(?<requirement>.+?)\s+version\s+of\s+(?<missing>[a-z0-9_.-]+),\s+which\s+is\s+missing")]
    private static partial Regex _FabricMissingDependencyRegex();

    [GeneratedRegex(
        @"(?i)HARD_DEP(?:_NO_CANDIDATE)?\s+(?<affected>[a-z0-9_.-]+)\s+[^\{\]]*\{depends\s+(?<missing>[a-z0-9_.-]+)\s+@\s+\[(?<version>[^\]]+)\]")]
    private static partial Regex _HardDependencyRegex();

    [GeneratedRegex(@"(?i)add:(?<missing>[a-z0-9_.-]+)\s+(?<version>[^\]\s]+)")]
    private static partial Regex _FixAddRegex();

    [GeneratedRegex(
        @"(?i)\bNEG_HARD_DEP\s+(?<source>[a-z0-9_.-]+)(?:\s+[^\{\]\s]+)?\s+\{breaks\s+(?<target>[a-z0-9_.-]+)\s+@\s+\[(?<range>[^\]]+)\]\}")]
    private static partial Regex _FabricBreaksConflictRegex();

    [GeneratedRegex(@"(?i)^\s*Fix:\s*(?<value>.+)$")]
    private static partial Regex _FixRegex();

    [GeneratedRegex(@"(?i)which\s+is\s+missing|missing dependency|requires.*missing")]
    private static partial Regex _MissingDependencyRegex();

    [GeneratedRegex(@"^\s*(?:at\s|Caused by:|Exception in thread|--\s|\[\d{2}:\d{2}:\d{2})")]
    private static partial Regex _HardStopRegex();

    private sealed record ResolutionBlock(string Text, int StartIndex, int EndIndex);
}