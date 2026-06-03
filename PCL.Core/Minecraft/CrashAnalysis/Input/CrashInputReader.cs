using System.IO.Compression;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     崩溃日志输入读取器。这里只发现、读取、分类日志，不做诊断。
/// </summary>
public sealed class CrashInputReader
{
    public static CrashLogBundle Read(CrashAnalysisRequest request)
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

        return new CrashLogBundle
        {
            Documents = ordered,
            Windows = ordered.Select(CrashLogWindow.Create).ToList()
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
                LastWriteTime = request.Now,
                Text = string.Join('\n', request.CapturedOutputLines)
            });

        if (!string.IsNullOrWhiteSpace(request.LaunchScript))
            result.Add(new CrashLogDocument
            {
                Kind = CrashLogKind.LaunchScript,
                Name = "launch-script.bat",
                Origin = CrashLogOrigin.Generated,
                LastWriteTime = request.Now,
                Text = request.LaunchScript
            });

        result.AddRange(_DiscoverLivePaths(request)
            .Take(CrashInputOptions.MaxLiveCandidateCount)
            .Select(path => _ReadFile(path, CrashLogOrigin.FileSystem, _ClassifyByName(path)))
            .OfType<CrashLogDocument>());

        return result;
    }

    private static IEnumerable<string> _DiscoverLivePaths(CrashAnalysisRequest request)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in _CandidateDirectories(request))
        {
            foreach (var path in _Enumerate(directory, "*.log"))
                if (_IsRecent(path, request.Now) && seen.Add(path))
                    yield return path;
            foreach (var path in _Enumerate(directory, "*.txt"))
                if (_IsRecent(path, request.Now) && seen.Add(path))
                    yield return path;
        }

        if (!string.IsNullOrWhiteSpace(request.InstancePath))
        {
            foreach (var path in _Enumerate(Path.Combine(request.InstancePath, "crash-reports"), "*.txt"))
                if (seen.Add(path))
                    yield return path;
            foreach (var file in new[] { "latest.log", "debug.log" })
            {
                var path = Path.Combine(request.InstancePath, "logs", file);
                if (File.Exists(path) && seen.Add(path)) yield return path;
            }
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
        var targetRoot = string.IsNullOrWhiteSpace(tempDirectory)
            ? Path.Combine(Path.GetTempPath(), "pcl-crash-import-" + Guid.NewGuid().ToString("N"))
            : Path.Combine(tempDirectory, "crash-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetRoot);

        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            if (!_IsReadableLog(entry.FullName)) continue;
            if (result.Count >= CrashInputOptions.MaxArchiveLogCount) break;
            if (entry.Length > CrashInputOptions.MaxSingleLogBytes) continue;
            totalBytes += entry.Length;
            if (totalBytes > CrashInputOptions.MaxArchiveBytes) break;

            var targetPath = Path.GetFullPath(Path.Combine(targetRoot, entry.FullName));
            if (!_IsSubPathOf(targetPath, targetRoot)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, true);
            var document = _ReadFile(targetPath, CrashLogOrigin.ImportedArchive, _ClassifyByName(entry.FullName));
            if (document is not null)
                result.Add(document with { Name = entry.FullName.Replace('\\', '/') });
        }

        return result;
    }

    private static bool _IsSubPathOf(string childPath, string parentPath)
    {
        var child = Path.GetFullPath(childPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parent = Path.GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }

    private static CrashLogDocument? _ReadFile(string path, CrashLogOrigin origin, CrashLogKind kind)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > CrashInputOptions.MaxSingleLogBytes) return null;
            if (!_IsReadableLog(path)) return null;
            return new CrashLogDocument
            {
                Kind = kind,
                Name = info.Name,
                FullPath = info.FullName,
                Origin = origin,
                LastWriteTime = info.LastWriteTime,
                OriginalLength = info.Length,
                Text = File.ReadAllText(info.FullName, Encoding.UTF8)
            };
        }
        catch
        {
            return null;
        }
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
            return now - time <= CrashInputOptions.RecentLogWindow;
        }
        catch
        {
            return false;
        }
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
}