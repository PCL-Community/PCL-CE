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
        var lines = CrashText.ReadLines(document.Text);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AppendJavaErrorFacts(facts, document, line, lineNumber);
            _AppendJavaVersionFact(facts, document, line, lineNumber);
            _AppendClassFileMajorFacts(facts, document, line, lineNumber);
            _AppendArchitectureFact(facts, document, line, lineNumber);
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
            properties));
    }

    private static void _AppendJavaErrorFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_Contains(line, "OutOfMemoryError"))
            facts.Add(_CreateLineFact(
                CrashFactKind.JavaOutOfMemoryDetected,
                document,
                line,
                lineNumber));

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

    private static void _AppendArchitectureFact(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
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

    [GeneratedRegex("""(?im)^\s*(?:java version|jre version|java runtime|java:)\s*[\"']?(?<value>\d+(?:\.\d+){0,3})""")]
    private static partial Regex _JavaVersionRegex();

    [GeneratedRegex(@"(?i)(?:class file version|class version|major version)\s+(?<major>\d{2,3})")]
    private static partial Regex _ClassFileMajorRegex();

    [GeneratedRegex(@"(?<major>\d+)(?:\.(?<legacy>\d+))?")]
    private static partial Regex _JavaVersionValueRegex();

    [GeneratedRegex(@"(?i)\b(?<arch>x86|amd64|x86_64|aarch64|arm64)\b")]
    private static partial Regex _ArchitectureRegex();
}