namespace PCL.Core.Minecraft.CrashAnalysis;

internal sealed partial class FileSystemCrashParser : ICrashLogParser
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
        var lines = CrashText.ReadLines(document.Text);

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            _AppendFileAccessFacts(facts, document, line, lineNumber);
            _AppendIntegrityFacts(facts, document, line, lineNumber);
            _AppendLibraryFacts(facts, document, line, lineNumber);
        }
    }

    private static void _AppendFileAccessFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_AccessDeniedRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.AccessDeniedDetected, document, line, lineNumber));

        if (_PathTooLongRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.PathTooLongDetected, document, line, lineNumber));

        if (_DiskFullRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.DiskFullDetected, document, line, lineNumber));
    }

    private static void _AppendIntegrityFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_GameJarMissingRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.GameJarMissingDetected, document, line, lineNumber));

        if (_AssetMissingRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.AssetMissingDetected, document, line, lineNumber));

        if (_ChecksumMismatchRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.ChecksumMismatchDetected, document, line, lineNumber));
    }

    private static void _AppendLibraryFacts(
        List<CrashFact> facts,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        if (_NativeLibraryMissingRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.NativeLibraryMissingDetected, document, line, lineNumber));

        if (_LibraryMissingRegex().IsMatch(line))
            facts.Add(_CreateLineFact(CrashFactKind.LibraryMissingDetected, document, line, lineNumber));
    }

    private static CrashFact _CreateLineFact(
        CrashFactKind kind,
        CrashLogDocument document,
        string line,
        int lineNumber)
    {
        var value = CrashText.SummarizeEvidence(line);
        return CrashFactFactory.Create(kind, value, document, line, lineNumber);
    }

    [GeneratedRegex(
        @"(?i)AccessDeniedException|Permission denied|access is denied|process cannot access the file|拒绝访问")]
    private static partial Regex _AccessDeniedRegex();

    [GeneratedRegex(@"(?i)PathTooLongException|path too long|file name too long")]
    private static partial Regex _PathTooLongRegex();

    [GeneratedRegex(@"(?i)No space left on device|not enough space on the disk|disk full|磁盘空间不足")]
    private static partial Regex _DiskFullRegex();

    [GeneratedRegex(
        @"(?i)Could not find or load main class\s+net\.minecraft\.client\.main\.Main|Unable to access jarfile.*(?:minecraft|client)|minecraft.*\.jar.*(?:missing|not found|does not exist)")]
    private static partial Regex _GameJarMissingRegex();

    [GeneratedRegex(
        @"(?i)(?:asset|assets)[^\n]*(?:missing|not found|hash mismatch|checksum mismatch)|Failed to download file.*assets")]
    private static partial Regex _AssetMissingRegex();

    [GeneratedRegex(@"(?i)hash mismatch|checksum mismatch|sha1 mismatch|expected hash|file integrity check failed")]
    private static partial Regex _ChecksumMismatchRegex();

    [GeneratedRegex(
        @"(?i)UnsatisfiedLinkError|no lwjgl.*java\.library\.path|Could not extract native|failed to load.*(?:native|lwjgl)|can't load library")]
    private static partial Regex _NativeLibraryMissingRegex();

    [GeneratedRegex(
        @"(?i)ClassNotFoundException:.*(?:lwjgl|launchwrapper|modlauncher)|NoClassDefFoundError:.*(?:lwjgl|launchwrapper|modlauncher)|missing library|library.*not found|Could not find library")]
    private static partial Regex _LibraryMissingRegex();
}