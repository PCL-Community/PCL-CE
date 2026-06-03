namespace PCL.Core.Minecraft.CrashAnalysis;

internal sealed partial class LoaderLogParser : ICrashLogParser
{
    public IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();

        foreach (var document in bundle.Documents)
            _AppendDocumentFacts(facts, document);

        _AppendRuntimeFacts(facts, request);

        return facts;
    }

    private static void _AppendDocumentFacts(List<CrashFact> facts, CrashLogDocument document)
    {
        var lines = CrashText.ReadLines(document.Text);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;
            var window = CrashText.GetWindow(lines, index, 3, 3);

            _AppendLoaderFact(facts, document, line, lineNumber);
            _AppendWindowFacts(facts, document, window, lineNumber);
        }
    }

    private static void _AppendRuntimeFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        var loaderName = request.RuntimeContext.LoaderName;

        if (string.IsNullOrWhiteSpace(loaderName))
            return;

        facts.Add(CrashFactFactory.CreateFromContext(
            CrashFactKind.LoaderDetected,
            loaderName,
            visibility: CrashFactVisibility.Technical));
    }

    private static void _AppendLoaderFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var match = _LoaderWithVersionRegex().Match(line);

        if (match.Success)
        {
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.LoaderDetected,
                _Join(match.Groups["loader"].Value, match.Groups["version"].Value),
                document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.High,
                visibility: CrashFactVisibility.Technical));
            return;
        }

        match = _LoaderRegex().Match(line);

        if (!match.Success)
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.LoaderDetected,
            match.Groups["loader"].Value,
            document,
            line,
            lineNumber,
            confidence: CrashFactConfidence.Medium,
            visibility: CrashFactVisibility.Technical));
    }

    private static void _AppendWindowFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string window,
        int lineNumber)
    {
        var dependencyProperties = _ExtractDependencyProperties(window);

        _AppendDependencyFacts(facts, document, window, lineNumber, dependencyProperties);
        _AppendConflictFacts(facts, document, window, lineNumber, dependencyProperties);
        _AppendDuplicateModFact(facts, document, window, lineNumber);
        _AppendMixinFact(facts, document, window, lineNumber);
        _AppendTransformFact(facts, document, window, lineNumber);
    }

    private static void _AppendDependencyFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string window,
        int lineNumber,
        IReadOnlyDictionary<string, string> dependencyProperties)
    {
        var hasPreciseDependency = dependencyProperties.ContainsKey("MissingModId")
                                   || _FabricHardDependencyRegex().IsMatch(window);

        if (_ResolutionFailedRegex().IsMatch(window))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.LoaderResolutionError,
                _Summary(window),
                document,
                window,
                lineNumber,
                dependencyProperties,
                visibility: CrashFactVisibility.Main));

        if (!hasPreciseDependency && !_IsDependencyWindow(window))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.MissingModDependencyDetected,
            _DependencySummary(window, dependencyProperties),
            document,
            window,
            lineNumber,
            dependencyProperties,
            visibility: CrashFactVisibility.Main));
    }

    private static void _AppendConflictFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string window,
        int lineNumber,
        IReadOnlyDictionary<string, string> dependencyProperties)
    {
        if (!_IncompatibleRegex().IsMatch(window))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.ModVersionConflictDetected,
            _Summary(window),
            document,
            window,
            lineNumber,
            dependencyProperties,
            visibility: CrashFactVisibility.Main));
    }

    private static void _AppendDuplicateModFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string window,
        int lineNumber)
    {
        if (!_DuplicateModRegex().IsMatch(window))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.DuplicateModDetected,
            _Summary(window),
            document,
            window,
            lineNumber));
    }

    private static void _AppendMixinFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string window,
        int lineNumber)
    {
        if (!_MixinFailureRegex().IsMatch(window))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.LoaderMixinError,
            _Summary(window),
            document,
            window,
            lineNumber,
            visibility: CrashFactVisibility.Main));
    }

    private static void _AppendTransformFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string window,
        int lineNumber)
    {
        if (!_TransformFailureRegex().IsMatch(window))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.LoaderTransformError,
            _Summary(window),
            document,
            window,
            lineNumber,
            visibility: CrashFactVisibility.Main));
    }

    private static bool _IsDependencyWindow(string window)
    {
        return (_MissingDependencyRegex().IsMatch(window) || _RequiresRegex().IsMatch(window))
               && (_MissingTokenRegex().IsMatch(window) || _InstallTokenRegex().IsMatch(window));
    }

    private static IReadOnlyDictionary<string, string> _ExtractDependencyProperties(string window)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        _AppendFabricMissingDependencyProperties(properties, window);
        _AppendFabricHardDependencyProperties(properties, window);
        _AppendFabricFixProperties(properties, window);
        _AppendLoaderProperties(properties, window);

        return properties;
    }

    private static void _AppendFabricMissingDependencyProperties(
        Dictionary<string, string> properties,
        string window)
    {
        var match = _FabricMissingDependencyRegex().Match(window);

        if (!match.Success)
            return;

        properties["AffectedMod"] = match.Groups["display"].Value.Trim();
        properties["AffectedModId"] = match.Groups["affected"].Value.Trim();
        properties["AffectedModVersion"] = match.Groups["affectedVersion"].Value.Trim();
        properties["MissingModId"] = match.Groups["missing"].Value.Trim();
        properties["RequiredVersion"] = _NormalizeRequirement(match.Groups["requirement"].Value);
    }

    private static void _AppendFabricHardDependencyProperties(
        Dictionary<string, string> properties,
        string window)
    {
        var match = _FabricHardDependencyRegex().Match(window);

        if (!match.Success)
            return;

        properties.TryAdd("AffectedModId", match.Groups["affected"].Value.Trim());
        properties.TryAdd("MissingModId", match.Groups["missing"].Value.Trim());
        properties.TryAdd("RequiredVersion", match.Groups["version"].Value.Trim());
    }

    private static void _AppendFabricFixProperties(Dictionary<string, string> properties, string window)
    {
        var match = _FabricFixAddRegex().Match(window);

        if (!match.Success)
            return;

        properties.TryAdd("MissingModId", match.Groups["missing"].Value.Trim());
        properties.TryAdd("RequiredVersion", _NormalizeFabricFixVersion(match.Groups["version"].Value));
    }

    private static void _AppendLoaderProperties(Dictionary<string, string> properties, string window)
    {
        var match = _LoaderWithVersionRegex().Match(window);

        if (!match.Success)
            return;

        properties.TryAdd("LoaderName", _Join(match.Groups["loader"].Value, match.Groups["version"].Value));
    }

    private static string _DependencySummary(string window, IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("AffectedMod", out var affected)
            && properties.TryGetValue("MissingModId", out var missing)
            && properties.TryGetValue("RequiredVersion", out var version))
            return affected + " requires " + missing + " " + version + ", but it is missing.";

        if (properties.TryGetValue("AffectedModId", out var affectedId)
            && properties.TryGetValue("MissingModId", out var missingId))
            return affectedId + " requires " + missingId + ", but it is missing.";

        return _Summary(window);
    }

    private static string _Summary(string value)
    {
        var line = CrashText.ReadLines(value)
            .Select(static item => item.Trim())
            .FirstOrDefault(static item => !string.IsNullOrWhiteSpace(item)
                                           && !item.StartsWith("at ", StringComparison.OrdinalIgnoreCase));

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

        if (value.EndsWith('-'))
            value = value.TrimEnd('-') + ".x";

        return value;
    }

    private static string _Join(string loader, string version)
    {
        loader = loader.Trim();
        version = version.Trim();

        return string.IsNullOrWhiteSpace(version) ? loader : loader + " " + version;
    }

    [GeneratedRegex(
        @"(?i)\b(?<loader>fabric|forge|neoforge|quilt|liteloader)\s+(?:loader\s+)?(?<version>\d+(?:\.\d+){1,3}[\w.+-]*)\b")]
    private static partial Regex _LoaderWithVersionRegex();

    [GeneratedRegex(@"(?i)\b(?<loader>fabric|forge|neoforge|quilt|liteloader)\b")]
    private static partial Regex _LoaderRegex();

    [GeneratedRegex(
        @"(?i)\bmissing\s+(dependency|dependencies|required\s+mod)\b|which\s+is\s+missing|but\s+it\s+is\s+not\s+installed")]
    private static partial Regex _MissingDependencyRegex();

    [GeneratedRegex(@"(?i)\bdepends?\s+on\b|\brequires\b|requires\s+any\s+version\s+of")]
    private static partial Regex _RequiresRegex();

    [GeneratedRegex(
        @"(?i)\bmod\s+resolution\s+failed\b|\bcould\s+not\s+resolve\b|\bfailed\s+to\s+resolve\b|HARD_DEP_NO_CANDIDATE")]
    private static partial Regex _ResolutionFailedRegex();

    [GeneratedRegex(
        @"(?i)\bincompatible\b.*\b(mod|version|minecraft|loader)\b|\bwrong\s+(minecraft|loader)\s+version\b")]
    private static partial Regex _IncompatibleRegex();

    [GeneratedRegex(
        @"(?i)\bmixin\b.*\b(apply|transform|injection)\b.*\b(fail|error|exception)\b|\bmixin\b.*\b(fail|error|exception)\b")]
    private static partial Regex _MixinFailureRegex();

    [GeneratedRegex(@"(?i)\btransform(er|ation)?\b.*\b(fail|error|exception)\b")]
    private static partial Regex _TransformFailureRegex();

    [GeneratedRegex(@"(?i)\bduplicate\b.*\bmod\b|\bmod\b.*\bduplicate\b")]
    private static partial Regex _DuplicateModRegex();

    [GeneratedRegex(@"(?i)\bmissing\b|which\s+is\s+missing|not\s+installed|install\s+it")]
    private static partial Regex _MissingTokenRegex();

    [GeneratedRegex(@"(?i)install|dependency|depends|requires|required")]
    private static partial Regex _InstallTokenRegex();

    [GeneratedRegex(
        @"(?i)Mod\s+'(?<display>[^']+)'\s+\((?<affected>[a-z0-9_.-]+)\)\s+(?<affectedVersion>[^\s]+)\s+requires\s+(?<requirement>.+?)\s+version\s+of\s+(?<missing>[a-z0-9_.-]+),\s+which\s+is\s+missing")]
    private static partial Regex _FabricMissingDependencyRegex();

    [GeneratedRegex(
        @"(?i)HARD_DEP(?:_NO_CANDIDATE)?\s+(?<affected>[a-z0-9_.-]+)\s+[^\{\]]*\{depends\s+(?<missing>[a-z0-9_.-]+)\s+@\s+\[(?<version>[^\]]+)\]")]
    private static partial Regex _FabricHardDependencyRegex();

    [GeneratedRegex(@"(?i)add:(?<missing>[a-z0-9_.-]+)\s+(?<version>[^\]\s]+)")]
    private static partial Regex _FabricFixAddRegex();
}