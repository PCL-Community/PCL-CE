using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashLogCollector
{
    public const long MaxSingleLogBytes = 32L * 1024L * 1024L;
    private const int MaxCandidateFiles = 64;
    private static readonly TimeSpan _RecentLogWindow = TimeSpan.FromMinutes(3);

    public static IReadOnlyList<CrashLogFile> Collect(CrashAnalysisRequest request)
    {
        var paths = _DiscoverRecentLogs(request)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxCandidateFiles);

        var files = paths
            .Select(path => _TryReadRecentFile(path, request, CrashLogOrigin.FileSystem))
            .OfType<CrashLogFile>()
            .ToList();

        if (request.LatestOutputLines.Count > 0)
            files.Add(new CrashLogFile
            {
                DisplayName = "RawOutput.log",
                Origin = CrashLogOrigin.CapturedOutput,
                LastWriteTime = request.Now,
                Content = string.Join("\n", request.LatestOutputLines),
                Length = request.LatestOutputLines.Sum(static line => line.Length)
            });

        return files;
    }

    private static IEnumerable<string> _DiscoverRecentLogs(CrashAnalysisRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.VersionPath))
        {
            foreach (var path in _EnumerateFiles(Path.Combine(request.VersionPath, "crash-reports"), "*.*"))
                yield return path;

            foreach (var path in _EnumerateFiles(request.VersionPath, "*.log"))
                yield return path;

            yield return Path.Combine(request.VersionPath, "logs", "latest.log");

            if (request.LatestLaunchScript?.Contains("-Dlog4j2.formatMsgNoLookups=false",
                    StringComparison.OrdinalIgnoreCase) ==
                true) yield return Path.Combine(request.VersionPath, "logs", "debug.log");
        }

        if (string.IsNullOrWhiteSpace(request.MinecraftRootPath)) yield break;

        foreach (var path in _EnumerateFiles(request.MinecraftRootPath, "*.log"))
            yield return path;
    }

    private static IEnumerable<string> _EnumerateFiles(string? directory, string pattern)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) yield break;

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            yield break;
        }

        foreach (var file in files) yield return file;
    }

    internal static CrashLogFile? _TryReadRecentFile(
        string path,
        CrashAnalysisRequest request,
        CrashLogOrigin origin)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaxSingleLogBytes) return null;

            var lastWriteTime = new DateTimeOffset(info.LastWriteTime);
            return (request.Now - lastWriteTime).Duration() > _RecentLogWindow
                ? null
                : TryReadFile(path, origin);
        }
        catch
        {
            return null;
        }
    }

    internal static CrashLogFile? TryReadFile(string path, CrashLogOrigin origin)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 || info.Length > MaxSingleLogBytes) return null;

            var content = File.ReadAllText(path, Encoding.UTF8);
            return new CrashLogFile
            {
                DisplayName = info.Name,
                FullPath = info.FullName,
                Origin = origin,
                LastWriteTime = new DateTimeOffset(info.LastWriteTime),
                Length = info.Length,
                Content = content
            };
        }
        catch
        {
            return null;
        }
    }
}