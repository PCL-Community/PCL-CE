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
        var lines = document.Lines;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AddAccessViolationFactIfMatched(facts, document, line, lineNumber);
        }

        _AddProblematicFrameFactIfMatched(facts, document, lines);
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
            CrashFactKind.JavaFatalErrorDetected,
            line.Trim(),
            document,
            line,
            lineNumber,
            visibility: CrashFactVisibility.Main));
        facts.Add(CrashFactFactory.Create(
            CrashFactKind.NativeAccessViolationDetected,
            line.Trim(),
            document,
            line,
            lineNumber,
            visibility: CrashFactVisibility.Main));
    }

    private static void _AddProblematicFrameFactIfMatched(
        List<CrashFact> facts,
        CrashLogDocument document,
        IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (!lines[index].Contains("problematic frame", StringComparison.OrdinalIgnoreCase))
                continue;

            var frameLine = _FindProblematicFrameLine(lines, index);
            if (frameLine is null)
                return;

            var (line, lineNumber) = frameLine.Value;
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.NativeProblematicFrameDetected,
                line.Trim(),
                document,
                line,
                lineNumber,
                visibility: CrashFactVisibility.Main,
                strength: CrashFactStrength.Direct,
                scope: CrashFactScope.Symptom));

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
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.GpuNativeLibraryCrashDetected,
                library,
                document,
                line,
                lineNumber));
            return;
        }
    }

    private static (string Line, int LineNumber)? _FindProblematicFrameLine(
        IReadOnlyList<string> lines,
        int headerIndex)
    {
        for (var index = headerIndex + 1; index < Math.Min(lines.Count, headerIndex + 6); index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith('#'))
                line = line.TrimStart('#').Trim();
            if (!string.IsNullOrWhiteSpace(line))
                return (line, index + 1);
        }

        return null;
    }

    private static bool _IsAccessViolationLine(string line)
    {
        return line.Contains("EXCEPTION_ACCESS_VIOLATION", StringComparison.OrdinalIgnoreCase)
               || line.Contains("SIGSEGV", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _IsGpuDriverLibrary(string library)
    {
        var name = Path.GetFileName(library).ToLowerInvariant();
        return name.StartsWith("nvoglv", StringComparison.Ordinal)
               || name.StartsWith("ig4icd", StringComparison.Ordinal)
               || name.StartsWith("ig7icd", StringComparison.Ordinal)
               || name.StartsWith("igd", StringComparison.Ordinal)
               || name is "atio6axx.dll"
                   or "atig6pxx.dll"
                   or "amdvlk64.dll"
                   or "amdvlk32.dll"
                   or "libgl.so"
                   or "libgl.so.1"
               || name.StartsWith("libnvidia-glcore.so", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<library>[a-z0-9_\-./\\]+\.(?:dll|so(?:\.\d+)?|dylib))", RegexOptions.IgnoreCase)]
    private static partial Regex _NativeLibraryRegex();
}