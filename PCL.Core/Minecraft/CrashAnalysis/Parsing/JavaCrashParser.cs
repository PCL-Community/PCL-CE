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
            _AppendArchitectureFact(facts, document, line, lineNumber);
        }
    }

    private static void _AppendRuntimeFacts(List<CrashFact> facts, CrashAnalysisRequest request)
    {
        var javaInfo = request.RuntimeContext.JavaInfo;

        if (string.IsNullOrWhiteSpace(javaInfo))
            return;

        facts.Add(CrashFactFactory.CreateFromContext(
            CrashFactKind.JavaVersionDetected,
            javaInfo,
            visibility: CrashFactVisibility.Technical));
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
        if (_Contains(line, ".java:"))
            return;

        var match = _JavaVersionRegex().Match(line);

        if (!match.Success)
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.JavaVersionDetected,
            match.Groups["value"].Value,
            document,
            line,
            lineNumber,
            visibility: CrashFactVisibility.Technical));
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

    private static string _TrimValue(string value)
    {
        value = value.Trim();
        return value.Length > MaxValueLength ? value[..MaxValueLength] : value;
    }

    [GeneratedRegex("""(?im)^\s*(?:java version|jre version|java runtime|java:)\s*["']?(?<value>\d+(?:\.\d+){0,3})""")]
    private static partial Regex _JavaVersionRegex();

    [GeneratedRegex(@"(?i)\b(?<arch>x86|amd64|x86_64|aarch64|arm64)\b")]
    private static partial Regex _ArchitectureRegex();
}