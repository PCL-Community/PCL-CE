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
            _AppendWindowFacts(facts, document, line, window, lineNumber);
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
        string line,
        string window,
        int lineNumber)
    {
        var dependencyProperties = _ExtractDependencyProperties(window);

        _AppendDependencyFacts(facts, document, line, window, lineNumber, dependencyProperties);
        _AppendConflictFacts(facts, document, line, window, lineNumber, dependencyProperties);
        _AppendForgeFacts(facts, document, line, window, lineNumber, dependencyProperties);
        _AppendLoaderVersionFacts(facts, document, line, window, lineNumber, dependencyProperties);
        _AppendDuplicateModFact(facts, document, line, window, lineNumber);
        _AppendMixinFact(facts, document, line, window, lineNumber);
        _AppendTransformFact(facts, document, line, window, lineNumber);
    }

    private static void _AppendDependencyFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        string window,
        int lineNumber,
        IReadOnlyDictionary<string, string> dependencyProperties)
    {
        var hasPreciseDependency = _FabricMissingDependencyRegex().IsMatch(line)
                                   || _FabricHardDependencyRegex().IsMatch(line)
                                   || (dependencyProperties.ContainsKey("MissingModId") && _IsDependencyWindow(line));

        if (_ResolutionFailedRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.LoaderResolutionError,
                _Summary(window),
                document,
                window,
                lineNumber,
                dependencyProperties,
                visibility: CrashFactVisibility.Main));

        if (!hasPreciseDependency && !_IsDependencyWindow(line))
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
        string line,
        string window,
        int lineNumber,
        IReadOnlyDictionary<string, string> dependencyProperties)
    {
        if (!_IncompatibleRegex().IsMatch(line))
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

    private static void _AppendForgeFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        string window,
        int lineNumber,
        IReadOnlyDictionary<string, string> dependencyProperties)
    {
        if (_ForgeModLoadingErrorRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ForgeModLoadingErrorDetected,
                _Summary(window),
                document,
                window,
                lineNumber,
                dependencyProperties,
                visibility: CrashFactVisibility.Main));

        if (_ForgeModLoadingErrorRegex().IsMatch(line) || _LoaderModLoadingFailedRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.LoaderModLoadingFailed,
                _Summary(window),
                document,
                window,
                lineNumber,
                dependencyProperties,
                visibility: CrashFactVisibility.Main));

        if (_ForgeMissingMandatoryDependencyRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ForgeMissingMandatoryDependencyDetected,
                _Summary(window),
                document,
                window,
                lineNumber,
                dependencyProperties,
                visibility: CrashFactVisibility.Main));

        if (_ForgeLanguageProviderRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ForgeLanguageProviderMissingDetected,
                _Summary(window),
                document,
                window,
                lineNumber,
                dependencyProperties,
                visibility: CrashFactVisibility.Main));
    }

    private static void _AppendLoaderVersionFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        string window,
        int lineNumber,
        IReadOnlyDictionary<string, string> dependencyProperties)
    {
        if (!_LoaderVersionRequirementRegex().IsMatch(line) && !_IncompatibleRegex().IsMatch(line))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.LoaderVersionRequirementDetected,
            _Summary(window),
            document,
            window,
            lineNumber,
            dependencyProperties,
            visibility: CrashFactVisibility.Main));

        if (_ForgeVersionRequirementRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ForgeVersionRequirementDetected,
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
        string line,
        string window,
        int lineNumber)
    {
        if (!_DuplicateModRegex().IsMatch(line))
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
        string line,
        string window,
        int lineNumber)
    {
        if (!_MixinFailureRegex().IsMatch(line))
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
        string line,
        string window,
        int lineNumber)
    {
        if (!_TransformFailureRegex().IsMatch(line))
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
        _AppendForgeDependencyProperties(properties, window);
        _AppendForgeRequiresProperties(properties, window);
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

    private static void _AppendForgeDependencyProperties(
        Dictionary<string, string> properties,
        string window)
    {
        var match = _NeoForgeDependencyLineRegex().Match(window);

        if (!match.Success)
            return;

        properties.TryAdd("MissingModId", match.Groups["missing"].Value.Trim());
        properties.TryAdd("AffectedModId", match.Groups["affected"].Value.Trim());
        properties.TryAdd("RequiredVersion", match.Groups["version"].Value.Trim());
        var current = match.Groups["current"].Value.Trim().Trim('[', ']');
        properties.TryAdd("CurrentVersion",
            current.Equals("MISSING", StringComparison.OrdinalIgnoreCase) ? "missing" : current);
    }

    private static void _AppendForgeRequiresProperties(
        Dictionary<string, string> properties,
        string window)
    {
        var match = _ForgeRequiresLineRegex().Match(window);

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
        var lines = CrashText.ReadLines(value)
            .Select(static item => item.Trim())
            .Where(static item => !string.IsNullOrWhiteSpace(item)
                                  && !item.StartsWith("at ", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var line = lines.FirstOrDefault(static item =>
                       item.Contains("Mod ID:", StringComparison.OrdinalIgnoreCase) ||
                       item.Contains("requires", StringComparison.OrdinalIgnoreCase) ||
                       item.Contains("missing", StringComparison.OrdinalIgnoreCase) ||
                       item.Contains("Mod resolution failed", StringComparison.OrdinalIgnoreCase) ||
                       item.Contains("HARD_DEP", StringComparison.OrdinalIgnoreCase))
                   ?? lines.FirstOrDefault(static item =>
                       item.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
                       item.Contains("Exception", StringComparison.OrdinalIgnoreCase))
                   ?? lines.FirstOrDefault();

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

    [GeneratedRegex(
        @"(?i)found\s+duplicate\s+mods?|duplicate\s+mods?\s+found|duplicate\s+mod\s+id|\bmod\b.*\bis\s+present\b.*\band\b")]
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

    [GeneratedRegex(
        @"(?i)Mod ID:\s*'(?<missing>[a-z0-9_.-]+)'\s*,\s*Requested by:\s*'(?<affected>[a-z0-9_.-]+)'\s*,\s*Expected range:\s*'(?<version>[^']+)'\s*,\s*Actual version:\s*'(?<current>[^']+)'")]
    private static partial Regex _NeoForgeDependencyLineRegex();

    [GeneratedRegex(
        @"(?i)Mod\s+(?<affected>[a-z0-9_.-]+)\s+requires\s+(?<missing>[a-z0-9_.-]+)\s+(?<version>.+?)(?:$|Currently|Reason)")]
    private static partial Regex _ForgeRequiresLineRegex();

    [GeneratedRegex(@"(?i)add:(?<missing>[a-z0-9_.-]+)\s+(?<version>[^\]\s]+)")]
    private static partial Regex _FabricFixAddRegex();

    [GeneratedRegex(
        @"(?i)Mod loading error has occurred|net\.minecraftforge\.fml\.ModLoadingException|net\.neoforged\.fml\.ModLoadingException|failed to load mod file|error loading mods")]
    private static partial Regex _ForgeModLoadingErrorRegex();

    [GeneratedRegex(
        @"(?i)failed to load mod file|Mod loading error has occurred|error loading mods|ModLoadingException")]
    private static partial Regex _LoaderModLoadingFailedRegex();

    [GeneratedRegex(
        @"(?i)Missing(?: or unsupported)? mandatory dependencies|missing mandatory dependency|requires.*(?:forge|minecraft|neoforge).*(?:missing|not found)")]
    private static partial Regex _ForgeMissingMandatoryDependencyRegex();

    [GeneratedRegex(
        @"(?i)needs language provider|missing language provider|language provider\s+(?:javafml|fmlcore|fmlmod)")]
    private static partial Regex _ForgeLanguageProviderRegex();

    [GeneratedRegex(
        @"(?i)requires\s+(?:minecraft|forge|neoforge|fabric|quilt)\s*(?:version)?|wrong\s+(?:minecraft|loader)\s+version|incompatible.*(?:minecraft|loader|forge|fabric|neoforge)|needs.*(?:minecraft|forge|neoforge).*(?:version|\[)")]
    private static partial Regex _LoaderVersionRequirementRegex();

    [GeneratedRegex(@"(?i)requires\s+(?:forge|neoforge)|needs.*(?:forge|neoforge).*(?:version|\[)")]
    private static partial Regex _ForgeVersionRequirementRegex();
}