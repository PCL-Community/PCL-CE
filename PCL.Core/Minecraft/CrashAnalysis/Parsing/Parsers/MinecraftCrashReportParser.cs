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

        var lines = document.Lines;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AppendMinecraftVersionFact(facts, document, line, lineNumber);
            if (document.Kind != CrashLogKind.MinecraftCrashReport)
            {
                _AppendMainExceptionFact(facts, document, line, lineNumber);
                _AppendWorldIssueFacts(facts, document, line, lineNumber);
            }

            _AppendClientContentIssueFacts(facts, document, line, lineNumber);
            _AppendDataPackFacts(facts, document, line, lineNumber);
            _AppendRegistryFacts(facts, document, line, lineNumber);
        }
    }

    private static void _AppendRuntimeFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        var minecraftVersion = request.RuntimeContext.MinecraftVersion;

        if (string.IsNullOrWhiteSpace(minecraftVersion))
            return;

        facts.Add(CrashFactFactory.CreateFromContext(
            CrashFactKind.MinecraftVersionDetected,
            minecraftVersion,
            sourceKind: CrashLogKind.LauncherLog));
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
        if (!_LooksLikeCrashExceptionLine(line))
            return;

        var match = _ExceptionLineRegex().Match(line);

        if (!match.Success)
            return;

        var exceptionType = match.Groups["type"].Value;
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.MinecraftMainException,
            exceptionType,
            document,
            line,
            lineNumber,
            visibility: CrashFactVisibility.Technical));

        if (exceptionType.Contains("ReportedException", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Reported exception", StringComparison.OrdinalIgnoreCase))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.MinecraftReportedException,
                CrashText.SummarizeEvidence(line),
                document,
                line,
                lineNumber,
                visibility: CrashFactVisibility.Technical));
    }

    private static void _AppendWorldIssueFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var isBlockEntityIssue = _Contains(line, "Block entity being ticked") ||
                                 _Contains(line, "Block being ticked") ||
                                 _Contains(line, "Block location");
        if (isBlockEntityIssue)
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.WorldBlockEntityIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber));
        else if (_Contains(line, "Ticking entity") || _Contains(line, "Entity being ticked"))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.WorldEntityIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber));

        if (_WorldChunkFailureRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.WorldChunkLoadFailed,
                line.Trim(),
                document,
                line,
                lineNumber));

        if (_WorldNbtFailureRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.WorldNbtReadFailed,
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
        if (_OpenGlInitializationRegex().IsMatch(line))
        {
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.OpenGlInitializationFailed,
                line.Trim(),
                document,
                line,
                lineNumber));

            if (_GlfwErrorRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(CrashFactKind.GlfwErrorDetected, line.Trim(), document, line,
                    lineNumber));
            if (_OpenGlContextRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(CrashFactKind.OpenGlContextCreationFailed, line.Trim(), document,
                    line, lineNumber));
            if (_OpenGlVersionRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(CrashFactKind.OpenGlVersionTooLowDetected, line.Trim(), document,
                    line, lineNumber));
            if (_PixelFormatRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(CrashFactKind.PixelFormatNotAcceleratedDetected, line.Trim(),
                    document, line, lineNumber));
        }

        if (_LwjglInitializationRegex().IsMatch(line))
        {
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.LwjglInitializationFailed,
                line.Trim(),
                document,
                line,
                lineNumber));

            if (_LwjglNativeLoadRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(CrashFactKind.LwjglNativeLoadFailed, line.Trim(), document, line,
                    lineNumber));
        }

        if (_ResourcePackFailureRegex().IsMatch(line))
        {
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ResourcePackIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.Medium));

            if (_TextureAtlasRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(CrashFactKind.TextureAtlasTooLargeDetected, line.Trim(), document,
                    line, lineNumber));
        }

        if (_ShaderFailureRegex().IsMatch(line))
        {
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.ShaderIssueDetected,
                line.Trim(),
                document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.Medium));

            if (_ShaderCompileRegex().IsMatch(line))
                facts.Add(CrashFactFactory.Create(CrashFactKind.ShaderCompileFailedDetected, line.Trim(), document,
                    line, lineNumber));
        }
    }

    private static void _AppendDataPackFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (!_DataPackFailureRegex().IsMatch(line))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.DataPackLoadFailed,
            line.Trim(),
            document,
            line,
            lineNumber));
    }

    private static void _AppendRegistryFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (!_RegistryIssueRegex().IsMatch(line))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.RegistryEntryMissingDetected,
            line.Trim(),
            document,
            line,
            lineNumber));
    }

    private static bool _LooksLikeCrashExceptionLine(string line)
    {
        return line.Contains("Reported exception thrown", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Caused by:", StringComparison.OrdinalIgnoreCase)
               || line.Contains("[FATAL]", StringComparison.OrdinalIgnoreCase)
               || line.Contains("/FATAL", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Exception in thread", StringComparison.OrdinalIgnoreCase)
               || line.Contains("ReportedException", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _Contains(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?i)^\s*Minecraft\s+Version\s*:\s*(?<version>\d+\.\d+(?:\.\d+)?)\b")]
    private static partial Regex _MinecraftVersionRegex();

    [GeneratedRegex(@"(?<type>[a-zA-Z_][\w\.]+(?:Exception|Error))(?::|$)")]
    private static partial Regex _ExceptionLineRegex();

    [GeneratedRegex(
        @"(?i)GLFW error 65542|driver does not appear to support OpenGL|pixel format not accelerated|failed to create (?:window|OpenGL context)|no OpenGL context")]
    private static partial Regex _OpenGlInitializationRegex();

    [GeneratedRegex(
        @"(?i)LWJGLException|UnsatisfiedLinkError:.*(?:lwjgl|org\.lwjgl)|failed to load.*lwjgl|no lwjgl.*java\.library\.path")]
    private static partial Regex _LwjglInitializationRegex();

    [GeneratedRegex(
        @"(?i)resource reload failed|failed to reload resources|stitcherexception|texture atlas too large|out of memory.*texture|could not load texture")]
    private static partial Regex _ResourcePackFailureRegex();

    [GeneratedRegex(
        @"(?i)shader (?:compile|compilation|link) (?:failed|error)|failed to compile shader|OpenGL error 1282.*(?:shader|render)|(?:shader|render).*OpenGL error 1282")]
    private static partial Regex _ShaderFailureRegex();

    [GeneratedRegex(@"(?i)GLFW error")]
    private static partial Regex _GlfwErrorRegex();

    [GeneratedRegex(
        @"(?i)failed to create (?:window|OpenGL context)|no OpenGL context|could not create OpenGL context")]
    private static partial Regex _OpenGlContextRegex();

    [GeneratedRegex(
        @"(?i)OpenGL (?:version|profile).*too (?:old|low)|requires OpenGL|driver does not appear to support OpenGL")]
    private static partial Regex _OpenGlVersionRegex();

    [GeneratedRegex(@"(?i)pixel format not accelerated")]
    private static partial Regex _PixelFormatRegex();

    [GeneratedRegex(
        @"(?i)UnsatisfiedLinkError:.*(?:lwjgl|org\.lwjgl)|failed to load.*lwjgl|no lwjgl.*java\.library\.path")]
    private static partial Regex _LwjglNativeLoadRegex();

    [GeneratedRegex(@"(?i)shader (?:compile|compilation|link) (?:failed|error)|failed to compile shader")]
    private static partial Regex _ShaderCompileRegex();

    [GeneratedRegex(@"(?i)stitcherexception|texture atlas too large|out of memory.*texture")]
    private static partial Regex _TextureAtlasRegex();

    [GeneratedRegex(@"(?i)Exception loading chunk|Couldn't load chunk|failed to load chunk|chunk.*(?:failed|error)")]
    private static partial Regex _WorldChunkFailureRegex();

    [GeneratedRegex(@"(?i)NBTException|Failed to load level data|Unable to read NBT|nbt.*(?:failed|error)")]
    private static partial Regex _WorldNbtFailureRegex();

    [GeneratedRegex(
        @"(?i)failed to load datapacks|errors in currently selected datapacks|data pack validation failed|datapack.*(?:failed|error)|failed to validate datapack")]
    private static partial Regex _DataPackFailureRegex();

    [GeneratedRegex(
        @"(?i)missing registry|unknown registry key|unbound values in registry|registry remapping failed|missing key in ResourceKey|unknown registry element|failed to parse registry")]
    private static partial Regex _RegistryIssueRegex();
}