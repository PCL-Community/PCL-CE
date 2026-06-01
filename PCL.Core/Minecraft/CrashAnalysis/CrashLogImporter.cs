using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed class CrashLogImporter
{
    private const int MaxImportedFiles = 128;
    private const long MaxTotalExtractedBytes = 128L * 1024L * 1024L;

    public static IReadOnlyList<CrashLogFile> Import(CrashAnalysisRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ImportedFilePath) || !File.Exists(request.ImportedFilePath)) return [];

        return _LooksLikeArchive(request.ImportedFilePath)
            ? _ImportArchive(request.ImportedFilePath, request)
            : _ImportSingleFile(request.ImportedFilePath);
    }

    private static IReadOnlyList<CrashLogFile> _ImportSingleFile(string filePath)
    {
        if (!_IsLogFile(filePath)) return [];
        var logFile = CrashLogCollector.TryReadFile(filePath, CrashLogOrigin.ImportedFile);
        return logFile is null ? [] : [logFile];
    }

    private static IReadOnlyList<CrashLogFile> _ImportArchive(string archivePath, CrashAnalysisRequest request)
    {
        var logs = new List<CrashLogFile>();
        var destination = _GetImportDirectory(request);
        Directory.CreateDirectory(destination);

        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            long extractedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                if (logs.Count >= MaxImportedFiles) break;
                if (string.IsNullOrEmpty(entry.Name)) continue;
                if (!_IsLogFile(entry.Name)) continue;
                if (entry.Length is <= 0 or > CrashLogCollector.MaxSingleLogBytes) continue;

                extractedBytes += entry.Length;
                if (extractedBytes > MaxTotalExtractedBytes) break;

                var targetPath = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                if (!_IsSubPathOf(targetPath, destination)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                entry.ExtractToFile(targetPath, true);

                var content = File.ReadAllText(targetPath, Encoding.UTF8);
                logs.Add(new CrashLogFile
                {
                    DisplayName = entry.FullName.Replace('/', Path.DirectorySeparatorChar),
                    FullPath = targetPath,
                    Origin = CrashLogOrigin.ImportedArchive,
                    LastWriteTime = entry.LastWriteTime,
                    Length = entry.Length,
                    Content = content
                });
            }
        }
        catch
        {
            return [];
        }

        return logs;
    }

    private static string _GetImportDirectory(CrashAnalysisRequest request)
    {
        var root = string.IsNullOrWhiteSpace(request.TempDirectory)
            ? Path.Combine(Path.GetTempPath(), "PCL-CE-CrashAnalysis")
            : request.TempDirectory;
        return Path.GetFullPath(Path.Combine(root, "Import"));
    }

    private static bool _LooksLikeArchive(string filePath)
    {
        return string.Equals(Path.GetExtension(filePath), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _IsLogFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return string.Equals(extension, ".log", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool _IsSubPathOf(string childPath, string parentPath)
    {
        var child = Path
            .GetFullPath(childPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var parent = Path
            .GetFullPath(parentPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return child.StartsWith(parent, StringComparison.OrdinalIgnoreCase);
    }
}