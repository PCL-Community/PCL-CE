using System.IO.Compression;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     崩溃日志输入读取器。这里只发现、读取、分类日志，不做诊断。
/// </summary>
public sealed partial class CrashInputReader
{
    public const long MaxSingleLogBytes = 32L * 1024L * 1024L;
    public const long MaxArchiveBytes = 128L * 1024L * 1024L;
    public const int MaxArchiveLogCount = 128;
    public const int MaxLiveCandidateCount = 64;
    public const int MaxRecentSubDirectories = 48;
    public static readonly TimeSpan RecentLogWindow = TimeSpan.FromMinutes(3);
    
    public CrashLogBundle Read(CrashAnalysisRequest request)
    {
        var documents = request.Source switch
        {
            CrashAnalysisSource.LiveGame => _ReadLiveGame(request),
            CrashAnalysisSource.ImportedFile => _ReadImported(request),
            _ => []
        };

        var ordered = documents
            .Where(static document => !string.IsNullOrWhiteSpace(document.Text))
            .GroupBy(static document => document.FullPath ?? document.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        ordered = _ShouldAssignCurrentCrashRoles(ordered, request)
            ? _AssignLiveAnalysisRoles(ordered, request)
            : ordered.Select(static document => document with { AnalysisRole = CrashLogAnalysisRole.Primary }).ToList();

        return new CrashLogBundle
        {
            Documents = ordered,
            Windows = ordered
                .Where(static document => document.AnalysisRole != CrashLogAnalysisRole.ReportOnly)
                .Select(CrashLogWindow.Create)
                .ToList()
        };
    }

    private static IReadOnlyList<CrashLogDocument> _ReadLiveGame(CrashAnalysisRequest request)
    {
        var result = new List<CrashLogDocument>();
        if (request.CapturedOutputLines.Count > 0)
            result.Add(new CrashLogDocument
            {
                Kind = CrashLogKind.CapturedGameOutput,
                Name = "captured-output.log",
                Origin = CrashLogOrigin.CapturedOutput,
                AnalysisRole = CrashLogAnalysisRole.Primary,
                LastWriteTime = request.Now,
                Text = string.Join('\n', request.CapturedOutputLines)
            });

        if (!string.IsNullOrWhiteSpace(request.LaunchScript))
            result.Add(new CrashLogDocument
            {
                Kind = CrashLogKind.LaunchScript,
                Name = "launch-script.bat",
                Origin = CrashLogOrigin.Generated,
                AnalysisRole = CrashLogAnalysisRole.Supporting,
                LastWriteTime = request.Now,
                Text = request.LaunchScript
            });

        result.AddRange(_DiscoverLivePaths(request)
            .OrderByDescending(_SafeLastWriteTime)
            .Take(MaxLiveCandidateCount)
            .Select(path => _ReadFile(path, CrashLogOrigin.FileSystem, _ClassifyByName(path)))
            .OfType<CrashLogDocument>());

        return result;
    }


    private static bool _LooksLikePclCrashReportBundle(IReadOnlyList<CrashLogDocument> documents)
    {
        return documents.Any(static document => document.Kind is CrashLogKind.CapturedGameOutput
                   or CrashLogKind.MinecraftLatestLog
                   or CrashLogKind.MinecraftDebugLog)
               && documents.Any(static document => document.Kind is CrashLogKind.MinecraftCrashReport
                   or CrashLogKind.JavaFatalErrorLog);
    }

    private static bool _ShouldAssignCurrentCrashRoles(
        IReadOnlyList<CrashLogDocument> documents,
        CrashAnalysisRequest request)
    {
        return request.Source == CrashAnalysisSource.LiveGame
               || _LooksLikePclCrashReportBundle(documents)
               || _LooksLikeImportedDiagnosticBundle(documents, request);
    }

    private static bool _LooksLikeImportedDiagnosticBundle(
        IReadOnlyList<CrashLogDocument> documents,
        CrashAnalysisRequest request)
    {
        if (request.Source != CrashAnalysisSource.ImportedFile)
            return false;
        if (documents.All(static document => document.Origin != CrashLogOrigin.ImportedArchive))
            return false;

        return documents.Count(static document => document.Kind is CrashLogKind.MinecraftCrashReport
            or CrashLogKind.JavaFatalErrorLog) > 1;
    }

    private static List<CrashLogDocument> _AssignLiveAnalysisRoles(
        IReadOnlyList<CrashLogDocument> documents,
        CrashAnalysisRequest request)
    {
        var referencedNames = _ExtractReferencedReportNames(documents);
        var currentCrashName = referencedNames
            .FirstOrDefault(static name => name.StartsWith("crash-", StringComparison.OrdinalIgnoreCase));
        var currentHsErrName = referencedNames
            .FirstOrDefault(static name => name.StartsWith("hs_err", StringComparison.OrdinalIgnoreCase));

        var fallbackDiagnosticDocument = currentCrashName is null && currentHsErrName is null
            ? _SelectFallbackCurrentDiagnosticDocument(documents, request)
            : null;

        return documents.Select(document =>
        {
            var role = _DetermineRole(document, currentCrashName, currentHsErrName, fallbackDiagnosticDocument);
            return document with { AnalysisRole = role };
        }).ToList();
    }

    private static CrashLogAnalysisRole _DetermineRole(
        CrashLogDocument document,
        string? currentCrashName,
        string? currentHsErrName,
        CrashLogDocument? fallbackDiagnosticDocument)
    {
        return document.Kind switch
        {
            CrashLogKind.CapturedGameOutput =>
                CrashLogAnalysisRole.Primary,
            CrashLogKind.MinecraftLatestLog
                or CrashLogKind.MinecraftDebugLog
                or CrashLogKind.LaunchScript =>
                CrashLogAnalysisRole.Supporting,
            CrashLogKind.MinecraftCrashReport when _NameEquals(document, currentCrashName) =>
                CrashLogAnalysisRole.Primary,
            CrashLogKind.MinecraftCrashReport =>
                ReferenceEquals(document, fallbackDiagnosticDocument)
                    ? CrashLogAnalysisRole.Primary
                    : CrashLogAnalysisRole.ReportOnly,
            CrashLogKind.JavaFatalErrorLog =>
                _NameEquals(document, currentHsErrName) || ReferenceEquals(document, fallbackDiagnosticDocument)
                    ? CrashLogAnalysisRole.Primary
                    : CrashLogAnalysisRole.ReportOnly,
            _ => document.Origin == CrashLogOrigin.Generated
                ? CrashLogAnalysisRole.Supporting
                : CrashLogAnalysisRole.ReportOnly
        };
    }

    private static CrashLogDocument? _SelectFallbackCurrentDiagnosticDocument(
        IReadOnlyList<CrashLogDocument> documents,
        CrashAnalysisRequest request)
    {
        var candidates = documents
            .Where(static document => document.Kind is CrashLogKind.MinecraftCrashReport
                or CrashLogKind.JavaFatalErrorLog)
            .Select(document => new { Document = document, Time = _CrashReportSortTime(document) })
            .Where(item => item.Time is not null)
            .OrderByDescending(static item => item.Time)
            .ToList();

        if (candidates.Count == 0)
            return null;

        if (request.Source == CrashAnalysisSource.ImportedFile)
            return candidates[0].Document;

        var now = request.Now;
        return candidates
            .FirstOrDefault(item => now - item.Time!.Value <= RecentLogWindow)
            ?.Document;
    }

    private static DateTimeOffset? _CrashReportSortTime(CrashLogDocument document)
    {
        var nameMatch = _CrashReportNameTimeRegex().Match(document.Name);
        if (nameMatch.Success &&
            DateTimeOffset.TryParse(
                $"{nameMatch.Groups["date"].Value} {nameMatch.Groups["time"].Value.Replace('.', ':')}",
                out var nameTime))
            return nameTime;

        foreach (var line in document.Lines.Take(12))
        {
            var timeMatch = _CrashReportTimeRegex().Match(line);
            if (timeMatch.Success && DateTimeOffset.TryParse(timeMatch.Groups["time"].Value, out var reportTime))
                return reportTime;
        }

        return document.LastWriteTime;
    }

    private static bool _NameEquals(CrashLogDocument document, string? name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               string.Equals(document.Name, name, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> _ExtractReferencedReportNames(IReadOnlyList<CrashLogDocument> documents)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents.Where(static document =>
                     document.Kind is CrashLogKind.CapturedGameOutput
                         or CrashLogKind.MinecraftLatestLog
                         or CrashLogKind.MinecraftDebugLog))
        foreach (Match match in _ReferencedReportNameRegex().Matches(document.Text))
            names.Add(match.Groups["name"].Value);

        return names;
    }

    private static IEnumerable<string> _DiscoverLivePaths(CrashAnalysisRequest request)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.InstancePath))
        {
            foreach (var file in new[] { "latest.log", "debug.log" })
            {
                var path = Path.Combine(request.InstancePath, "logs", file);
                if (File.Exists(path) && seen.Add(path)) yield return path;
            }

            foreach (var path in _Enumerate(Path.Combine(request.InstancePath, "crash-reports"), "*.txt")
                         .OrderByDescending(_SafeLastWriteTime)
                         .Take(3))
                if (seen.Add(path))
                    yield return path;
        }

        foreach (var directory in _CandidateDirectories(request))
        {
            foreach (var path in _EnumerateRecent(directory, "*.log", request.Now))
                if (seen.Add(path))
                    yield return path;
            foreach (var path in _EnumerateRecent(directory, "*.txt", request.Now))
                if (seen.Add(path))
                    yield return path;
        }
    }

    private static IEnumerable<string> _CandidateDirectories(CrashAnalysisRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.InstancePath)) yield return request.InstancePath;
        if (!string.IsNullOrWhiteSpace(request.MinecraftRootPath)) yield return request.MinecraftRootPath;
    }

    private static IReadOnlyList<CrashLogDocument> _ReadImported(CrashAnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImportedFilePath) || !File.Exists(request.ImportedFilePath)) return [];
        if (_LooksLikeArchive(request.ImportedFilePath))
            return _ReadArchive(request.ImportedFilePath, request.TempDirectory);

        var document = _ReadFile(request.ImportedFilePath, CrashLogOrigin.ImportedFile,
            _ClassifyByName(request.ImportedFilePath));
        return document is null ? [] : [document];
    }

    private static IReadOnlyList<CrashLogDocument> _ReadArchive(string archivePath, string tempDirectory)
    {
        var result = new List<CrashLogDocument>();
        var totalBytes = 0L;

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries
                     .Where(static entry => !string.IsNullOrWhiteSpace(entry.Name))
                     .Where(static entry => _IsReadableLog(entry.FullName))
                     .OrderByDescending(static entry => entry.LastWriteTime))
        {
            if (result.Count >= MaxArchiveLogCount) break;
            if (entry.Length is <= 0 or > MaxSingleLogBytes) continue;
            totalBytes += entry.Length;
            if (totalBytes > MaxArchiveBytes) break;

            var text = _ReadZipEntryText(entry);
            if (string.IsNullOrWhiteSpace(text)) continue;

            result.Add(new CrashLogDocument
            {
                Kind = _ClassifyByName(entry.FullName),
                Name = entry.FullName.Replace('\\', '/'),
                FullPath = "archive://" + entry.FullName.Replace('\\', '/'),
                Origin = CrashLogOrigin.ImportedArchive,
                LastWriteTime = entry.LastWriteTime,
                OriginalLength = entry.Length,
                Text = text
            });
        }

        return result;
    }

    private static string? _ReadZipEntryText(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            return _ReadTextWithFallback(stream);
        }
        catch
        {
            return null;
        }
    }

    private static CrashLogDocument? _ReadFile(string path, CrashLogOrigin origin, CrashLogKind kind)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaxSingleLogBytes) return null;
            if (!_IsReadableLog(path)) return null;
            return new CrashLogDocument
            {
                Kind = kind,
                Name = info.Name,
                FullPath = info.FullName,
                Origin = origin,
                LastWriteTime = info.LastWriteTime,
                OriginalLength = info.Length,
                Text = _ReadTextWithFallback(info.FullName)
            };
        }
        catch
        {
            return null;
        }
    }

    private static string _ReadTextWithFallback(string path)
    {
        using var stream = File.OpenRead(path);
        return _ReadTextWithFallback(stream) ?? string.Empty;
    }

    private static string? _ReadTextWithFallback(Stream stream)
    {
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        if (bytes.Length == 0) return string.Empty;

        var bomEncoding = _DetectBomEncoding(bytes, out var bomLength);
        if (bomEncoding is not null)
            return _NormalizeNewLines(bomEncoding.GetString(bytes, bomLength, bytes.Length - bomLength));

        var strictUtf8 = new UTF8Encoding(false, true);
        try
        {
            return _NormalizeNewLines(strictUtf8.GetString(bytes));
        }
        catch (DecoderFallbackException)
        {
            foreach (var encoding in _FallbackEncodings())
                try
                {
                    return _NormalizeNewLines(encoding.GetString(bytes));
                }
                catch (DecoderFallbackException)
                {
                    // Try the next encoding.
                }

            return _NormalizeNewLines(Encoding.UTF8.GetString(bytes));
        }
    }

    private static Encoding? _DetectBomEncoding(byte[] bytes, out int bomLength)
    {
        switch (bytes.Length)
        {
            case >= 3 when bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF:
                bomLength = 3;
                return Encoding.UTF8;
            case >= 2 when bytes[0] == 0xFF && bytes[1] == 0xFE:
                bomLength = 2;
                return Encoding.Unicode;
            case >= 2 when bytes[0] == 0xFE && bytes[1] == 0xFF:
                bomLength = 2;
                return Encoding.BigEndianUnicode;
            default:
                bomLength = 0;
                return null;
        }
    }

    private static IEnumerable<Encoding> _FallbackEncodings()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        catch
        {
            // Code page provider is optional on some runtimes.
        }

        foreach (var name in new[] { "GB18030", "GBK", "GB2312", "windows-936" })
        {
            Encoding encoding;
            try
            {
                encoding = Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback,
                    DecoderFallback.ExceptionFallback);
            }
            catch
            {
                continue;
            }

            yield return encoding;
        }

        yield return Encoding.Default;
    }

    private static string _NormalizeNewLines(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static CrashLogKind _ClassifyByName(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();

        return name switch
        {
            _ when name.StartsWith("hs_err") => CrashLogKind.JavaFatalErrorLog,
            _ when name.StartsWith("crash-") => CrashLogKind.MinecraftCrashReport,

            "latest.log" or "latest log.txt" => CrashLogKind.MinecraftLatestLog,
            "debug.log" or "debug log.txt" => CrashLogKind.MinecraftDebugLog,
            "rawoutput.log" or "游戏崩溃前的输出.txt" => CrashLogKind.CapturedGameOutput,

            "启动器日志.txt"
                or "pcl2 启动器日志.txt"
                or "pcl 启动器日志.txt"
                or "log1.txt"
                or "log-ce1.log" => CrashLogKind.LauncherLog,

            _ => CrashLogKind.ImportedText
        };
    }

    private static bool _LooksLikeArchive(string path)
    {
        return string.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _IsReadableLog(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _IsRecent(string path, DateTimeOffset now)
    {
        try
        {
            var time = new FileInfo(path).LastWriteTime;
            return now - time <= RecentLogWindow;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> _EnumerateRecent(
        string? directory,
        string pattern,
        DateTimeOffset now)
    {
        foreach (var path in _Enumerate(directory, pattern))
            if (_IsRecent(path, now))
                yield return path;

        foreach (var subDirectory in _EnumerateDirectories(directory)
                     .Take(MaxRecentSubDirectories))
        foreach (var path in _Enumerate(subDirectory, pattern))
            if (_IsRecent(path, now))
                yield return path;
    }

    private static IEnumerable<string> _Enumerate(string? directory, string pattern)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) yield break;
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files) yield return file;
    }

    private static IEnumerable<string> _EnumerateDirectories(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) yield break;
        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (var item in directories) yield return item;
    }

    private static DateTime _SafeLastWriteTime(string path)
    {
        try
        {
            return new FileInfo(path).LastWriteTime;
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    [GeneratedRegex(@"(?i)crash-(?<date>\d{4}-\d{2}-\d{2})_(?<time>\d{2}\.\d{2}\.\d{2})-(?:client|server)\.txt")]
    private static partial Regex _CrashReportNameTimeRegex();

    [GeneratedRegex(@"(?i)^\s*Time:\s*(?<time>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})")]
    private static partial Regex _CrashReportTimeRegex();

    [GeneratedRegex(
        @"(?i)(?<name>crash-\d{4}-\d{2}-\d{2}_\d{2}\.\d{2}\.\d{2}-(?:client|server)\.txt|hs_err_pid\d+\.log)")]
    private static partial Regex _ReferencedReportNameRegex();
}