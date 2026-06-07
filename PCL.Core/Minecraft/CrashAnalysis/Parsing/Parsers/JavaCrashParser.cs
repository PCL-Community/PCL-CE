namespace PCL.Core.Minecraft.CrashAnalysis;

internal sealed partial class JavaCrashParser : ICrashLogParser
{
    private const int MaxValueLength = 240;

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
        var lines = document.Lines;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AppendManualDebugCrashFact(facts, document, line, lineNumber);
            _AppendJavaErrorFacts(facts, document, line, lineNumber);
            _AppendJavaLaunchFacts(facts, document, line, lineNumber);
            _AppendJavaVersionFact(facts, document, line, lineNumber);
            _AppendJavaVendorFact(facts, document, line, lineNumber);
            _AppendClassFileMajorFacts(facts, document, line, lineNumber);
            _AppendArchitectureFact(facts, document, line, lineNumber);
            _AppendMinecraftExitCodeFact(facts, document, line, lineNumber);
        }
    }

    private static void _AppendRuntimeFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        var javaInfo = request.RuntimeContext.JavaInfo;

        if (string.IsNullOrWhiteSpace(javaInfo))
            return;

        var properties = new Dictionary<string, string>();
        if (_TryExtractJavaMajor(javaInfo, out var major))
            properties["JavaMajor"] = major.ToString();

        facts.Add(CrashFactFactory.CreateFromContext(
            CrashFactKind.JavaVersionDetected,
            javaInfo,
            properties,
            sourceKind: CrashLogKind.LauncherLog));

        if (_JavaVendorRegex().Match(javaInfo) is { Success: true } vendor)
            facts.Add(CrashFactFactory.CreateFromContext(
                CrashFactKind.JavaVendorDetected,
                vendor.Groups["vendor"].Value,
                visibility: CrashFactVisibility.Technical,
                sourceKind: CrashLogKind.LauncherLog));
    }

    private static void _AppendManualDebugCrashFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (!_ManualDebugCrashRegex().IsMatch(line))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.ManualDebugCrashDetected,
            line.Trim(),
            document,
            line,
            lineNumber,
            visibility: CrashFactVisibility.Main,
            strength: CrashFactStrength.Direct,
            scope: CrashFactScope.RootCause));
    }

    private static void _AppendJavaErrorFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_Contains(line, "OutOfMemoryError"))
        {
            facts.Add(_CreateLineFact(
                CrashFactKind.JavaOutOfMemoryDetected,
                document,
                line,
                lineNumber));

            if (_Contains(line, "Java heap space"))
                facts.Add(_CreateLineFact(CrashFactKind.JavaHeapSpaceOutOfMemoryDetected, document, line, lineNumber));
            if (_Contains(line, "Metaspace"))
                facts.Add(_CreateLineFact(CrashFactKind.JavaMetaspaceOutOfMemoryDetected, document, line, lineNumber));
            if (_Contains(line, "Direct buffer memory"))
                facts.Add(
                    _CreateLineFact(CrashFactKind.JavaDirectBufferOutOfMemoryDetected, document, line, lineNumber));
            if (_Contains(line, "unable to create native thread"))
                facts.Add(
                    _CreateLineFact(CrashFactKind.JavaNativeThreadOutOfMemoryDetected, document, line, lineNumber));
            if (_Contains(line, "GC overhead limit exceeded"))
                facts.Add(_CreateLineFact(CrashFactKind.JavaGcOverheadDetected, document, line, lineNumber));
        }

        if (_Contains(line, "UnsupportedClassVersionError"))
            facts.Add(_CreateLineFact(
                CrashFactKind.JavaUnsupportedClassVersionDetected,
                document,
                line,
                lineNumber));

        if (_Contains(line, "InaccessibleObjectException") || _Contains(line, "module java.base"))
            facts.Add(_CreateLineFact(
                CrashFactKind.JavaModuleAccessErrorDetected,
                document,
                line,
                lineNumber));

        if (_Contains(line, "EXCEPTION_ACCESS_VIOLATION") || _Contains(line, "SIGSEGV"))
            facts.Add(_CreateLineFact(
                CrashFactKind.NativeAccessViolationDetected,
                document,
                line,
                lineNumber));

        if (_GenericJavaThrowableRegex().IsMatch(line))
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.MinecraftMainException,
                _TrimValue(line),
                document,
                line,
                lineNumber,
                visibility: CrashFactVisibility.Technical,
                strength: CrashFactStrength.Medium,
                scope: CrashFactScope.Symptom));
    }

    private static void _AppendJavaLaunchFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_JavaExecutableMissingRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.JavaExecutableMissingDetected, document, line, lineNumber));

        if (_JavaMainClassMissingRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.JavaMainClassMissingDetected, document, line, lineNumber));
    }

    private static void _AppendJavaVersionFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_IsStackTraceSourceLine(line))
            return;

        var match = _JavaVersionRegex().Match(line);

        if (!match.Success)
            return;

        var value = match.Groups["value"].Value;
        var properties = new Dictionary<string, string>();
        if (_TryExtractJavaMajor(value, out var major))
            properties["JavaMajor"] = major.ToString();

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.JavaVersionDetected,
            value,
            document,
            line,
            lineNumber,
            properties,
            visibility: CrashFactVisibility.Technical));
    }

    private static void _AppendJavaVendorFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var match = _JavaVendorRegex().Match(line);
        if (!match.Success)
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.JavaVendorDetected,
            match.Groups["vendor"].Value,
            document,
            line,
            lineNumber,
            visibility: CrashFactVisibility.Technical));
    }

    private static void _AppendClassFileMajorFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var match = _ClassFileMajorRegex().Match(line);

        if (!match.Success || !int.TryParse(match.Groups["major"].Value, out var classMajor))
            return;

        var requiredJava = _ClassFileMajorToJavaMajor(classMajor);
        var properties = new Dictionary<string, string>
        {
            ["ClassFileMajor"] = classMajor.ToString()
        };

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.JavaClassFileMajorVersionDetected,
            classMajor.ToString(),
            document,
            line,
            lineNumber,
            properties));

        if (requiredJava <= 0)
            return;

        properties = new Dictionary<string, string>
        {
            ["ClassFileMajor"] = classMajor.ToString(),
            ["RequiredJavaMajor"] = requiredJava.ToString()
        };

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.JavaRequiredVersionDetected,
            requiredJava.ToString(),
            document,
            line,
            lineNumber,
            properties));
    }

    private static void _AppendMinecraftExitCodeFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var match = _MinecraftExitCodeRegex().Match(line);
        if (!match.Success)
            return;

        var code = match.Groups["code"].Value.Trim();
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.MinecraftExitCodeDetected,
            code,
            document,
            line,
            lineNumber,
            new Dictionary<string, string> { ["ExitCode"] = code },
            visibility: CrashFactVisibility.Technical,
            strength: CrashFactStrength.Weak,
            scope: CrashFactScope.Symptom));
    }

    private static void _AppendArchitectureFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (!_LooksLikeJavaArchitectureLine(line))
            return;

        var match = _ArchitectureRegex().Match(line);

        if (!match.Success)
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.JavaArchitectureDetected,
            match.Groups["arch"].Value,
            document,
            line,
            lineNumber,
            visibility: CrashFactVisibility.Technical));
    }

    private static CrashFact _CreateLineFact(
        CrashFactKind kind,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        return CrashFactFactory.Create(
            kind,
            _TrimValue(line),
            document,
            line,
            lineNumber);
    }

    private static bool _LooksLikeJavaArchitectureLine(string line)
    {
        return line.Contains("Java VM", StringComparison.OrdinalIgnoreCase)
               || line.Contains("JVM", StringComparison.OrdinalIgnoreCase)
               || line.Contains("OpenJDK", StringComparison.OrdinalIgnoreCase)
               || line.Contains("Java architecture", StringComparison.OrdinalIgnoreCase)
               || line.Contains("VM info", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _Contains(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool _IsStackTraceSourceLine(string line)
    {
        return line.Contains(".java:", StringComparison.OrdinalIgnoreCase)
               || line.TrimStart().StartsWith("at ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _TryExtractJavaMajor(string value, out int major)
    {
        major = 0;
        var match = _JavaVersionValueRegex().Match(value);
        if (!match.Success || !int.TryParse(match.Groups["major"].Value, out var parsed))
            return false;

        major = parsed == 1 && int.TryParse(match.Groups["legacy"].Value, out var legacy)
            ? legacy
            : parsed;
        return major > 0;
    }

    private static int _ClassFileMajorToJavaMajor(int classMajor)
    {
        return classMajor >= 49 ? classMajor - 44 : 0;
    }

    private static string _TrimValue(string value)
    {
        value = value.Trim();
        return value.Length > MaxValueLength ? value[..MaxValueLength] : value;
    }

    [GeneratedRegex(@"(?i)Manually triggered debug crash|F3\s*\+\s*C")]
    private static partial Regex _ManualDebugCrashRegex();

    [GeneratedRegex(
        @"(?i)(?:exit(?:ed)?\s+(?:with\s+)?(?:code|value)|exit\s+code|process\s+crashed\s+with\s+exit\s+code)\s*[:=]?\s*(?<code>-?\d+)")]
    private static partial Regex _MinecraftExitCodeRegex();

    [GeneratedRegex(@"(?i)CreateProcess error=2|java(?:\.exe)?(?:'|\s)*(?:not found|does not exist|cannot find|找不到)")]
    private static partial Regex _JavaExecutableMissingRegex();

    [GeneratedRegex(
        @"(?i)Could not find or load main class|ClassNotFoundException:\s*net\.minecraft\.client\.main\.Main")]
    private static partial Regex _JavaMainClassMissingRegex();

    [GeneratedRegex(
        """(?im)^\s*#?\s*(?:java version|jre version|java runtime|java:)\s*:?\s*[^\d\r\n]*(?<value>\d+(?:\.\d+){0,3})""")]
    private static partial Regex _JavaVersionRegex();

    [GeneratedRegex(@"(?i)(?:class file version|class version|major version)\s+(?<major>\d{2,3})")]
    private static partial Regex _ClassFileMajorRegex();

    [GeneratedRegex(@"(?i)\b(?<vendor>OpenJ9|HotSpot|GraalVM|OpenJDK|Oracle)\b")]
    private static partial Regex _JavaVendorRegex();

    [GeneratedRegex(@"(?<major>\d+)(?:\.(?<legacy>\d+))?")]
    private static partial Regex _JavaVersionValueRegex();

    [GeneratedRegex(@"(?i)\b(?<arch>x86|amd64|x86_64|aarch64|arm64)\b")]
    private static partial Regex _ArchitectureRegex();

    [GeneratedRegex(@"^\s*(?:[A-Za-z_$][A-Za-z0-9_$]*\.)+[A-Za-z_$][A-Za-z0-9_$]*(?:Exception|Error)(?::|\b)")]
    private static partial Regex _GenericJavaThrowableRegex();
}