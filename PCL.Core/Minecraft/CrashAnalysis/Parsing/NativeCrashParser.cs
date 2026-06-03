namespace PCL.Core.Minecraft.CrashAnalysis;

internal sealed partial class NativeCrashParser : ICrashLogParser
{
    public IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();

        foreach (var document in bundle.Documents)
        {
            if (!_IsNativeCrashDocument(document))
                continue;

            facts.Add(CrashFactFactory.Create(
                CrashFactKind.JavaFatalErrorDetected,
                document.Name,
                document));

            _AppendLineFacts(facts, document);
        }

        return facts;
    }

    private static bool _IsNativeCrashDocument(CrashLogDocument document)
    {
        return document.Kind == CrashLogKind.JavaFatalErrorLog
               || document.Text.Contains("problematic frame", StringComparison.OrdinalIgnoreCase);
    }

    private static void _AppendLineFacts(List<CrashFact> facts, CrashLogDocument document)
    {
        var lines = CrashText.ReadLines(document.Text);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AddAccessViolationFactIfMatched(facts, document, line, lineNumber);
            _AddNativeLibraryFactIfMatched(facts, document, line, lineNumber);
        }
    }

    private static void _AddAccessViolationFactIfMatched(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (!_IsAccessViolationLine(line))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.NativeAccessViolationDetected,
            line.Trim(),
            document,
            line,
            lineNumber));
    }

    private static void _AddNativeLibraryFactIfMatched(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var match = _NativeLibraryRegex().Match(line);

        if (!match.Success)
            return;

        var library = match.Groups["library"].Value;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.NativeLibraryInCrashFrame,
            library,
            document,
            line,
            lineNumber));

        if (!_IsGpuDriverLibrary(library))
            return;

        facts.Add(CrashFactFactory.Create(
            CrashFactKind.GpuDriverIssueHint,
            library,
            document,
            line,
            lineNumber));
    }

    private static bool _IsAccessViolationLine(string line)
    {
        return line.Contains("EXCEPTION_ACCESS_VIOLATION", StringComparison.OrdinalIgnoreCase)
               || line.Contains("SIGSEGV", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _IsGpuDriverLibrary(string library)
    {
        return library.Contains("nvoglv", StringComparison.OrdinalIgnoreCase)
               || library.Contains("atio", StringComparison.OrdinalIgnoreCase)
               || library.Contains("ig", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?<library>[a-z0-9_\-]+\.(?:dll|so|dylib))", RegexOptions.IgnoreCase)]
    private static partial Regex _NativeLibraryRegex();
}