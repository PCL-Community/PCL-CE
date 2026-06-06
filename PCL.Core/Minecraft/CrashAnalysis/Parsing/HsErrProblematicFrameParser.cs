namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     专门解析 hs_err_pid 日志中的 problematic frame。显卡驱动判断只来自这个段落，
///     避免把普通路径或其他 native library 误识别为 GPU 崩溃。
/// </summary>
internal sealed partial class HsErrProblematicFrameParser : ICrashLogParser
{
    public IReadOnlyList<CrashFact> Parse(CrashLogBundle bundle, CrashAnalysisRequest request)
    {
        var facts = new List<CrashFact>();

        foreach (var document in bundle.Documents)
        {
            if (document.Kind != CrashLogKind.JavaFatalErrorLog &&
                !document.Text.Contains("problematic frame", StringComparison.OrdinalIgnoreCase))
                continue;

            _AppendDocumentFacts(facts, document);
        }

        return facts;
    }

    private static void _AppendDocumentFacts(List<CrashFact> facts, CrashLogDocument document)
    {
        var lines = CrashText.ReadLines(document.Text);
        for (var index = 0; index < lines.Count; index++)
        {
            if (!lines[index].Contains("problematic frame", StringComparison.OrdinalIgnoreCase))
                continue;

            var frame = _FindFrame(lines, index);
            if (frame is null) return;

            var (line, lineNumber) = frame.Value;
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
            if (!match.Success) return;

            var library = match.Groups["library"].Value.Trim();
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.NativeLibraryInCrashFrame,
                library,
                document,
                line,
                lineNumber,
                visibility: CrashFactVisibility.Main));

            if (!_IsGpuDriverLibrary(library)) return;

            facts.Add(CrashFactFactory.Create(
                CrashFactKind.GpuDriverIssueHint,
                library,
                document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.High,
                visibility: CrashFactVisibility.Main));
            facts.Add(CrashFactFactory.Create(
                CrashFactKind.GpuNativeLibraryCrashDetected,
                library,
                document,
                line,
                lineNumber,
                confidence: CrashFactConfidence.High,
                visibility: CrashFactVisibility.Main));
            return;
        }
    }

    private static (string Line, int LineNumber)? _FindFrame(IReadOnlyList<string> lines, int headerIndex)
    {
        for (var index = headerIndex + 1; index < Math.Min(lines.Count, headerIndex + 8); index++)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith('#'))
                line = line.TrimStart('#').Trim();
            if (!string.IsNullOrWhiteSpace(line)) return (line, index + 1);
        }

        return null;
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