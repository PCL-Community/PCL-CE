// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;

namespace PCL.Application.Downloads;

/// <summary>Installs a downloaded Minecraft world archive into a saves directory.</summary>
public static class MinecraftWorldArchiveInstaller
{
    private const long MaximumExpandedBytes = 16L * 1024L * 1024L * 1024L;

    public static async Task<string> InstallAsync(
        string archivePath,
        string savesDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(savesDirectory);

        string archiveFullPath = Path.GetFullPath(archivePath);
        string savesFullPath = Path.GetFullPath(savesDirectory);
        Directory.CreateDirectory(savesFullPath);

        using ZipArchive archive = ZipFile.OpenRead(archiveFullPath);
        string fallbackWorldName = Path.GetFileNameWithoutExtension(archiveFullPath);
        List<ArchiveFile> files = CollectWorldFiles(archive, fallbackWorldName);
        if (files.Count == 0)
            throw new InvalidDataException("压缩包中没有找到 Minecraft 世界（缺少 level.dat）。");

        long expandedBytes = 0L;
        foreach (ArchiveFile file in files)
        {
            expandedBytes = checked(expandedBytes + file.Entry.Length);
            if (expandedBytes > MaximumExpandedBytes)
                throw new InvalidDataException("世界压缩包解压后的体积超过 16 GiB，已停止安装。");
        }

        string preferredName = SanitizeDirectoryName(files[0].WorldName);
        string destination = GetUniqueDestination(savesFullPath, preferredName);
        string temporary = Path.Combine(savesFullPath, ".pcln-world-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);

        try
        {
            foreach (ArchiveFile file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string targetPath = GetContainedPath(temporary, file.RelativePath);
                string? parent = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                await using Stream source = file.Entry.Open();
                await using FileStream target = new(
                    targetPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    useAsync: true);
                await source.CopyToAsync(target, 64 * 1024, cancellationToken).ConfigureAwait(false);
            }

            Directory.Move(temporary, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, recursive: true);
            throw;
        }
    }

    private static List<ArchiveFile> CollectWorldFiles(ZipArchive archive, string fallbackWorldName)
    {
        List<(ZipArchiveEntry Entry, string Path)> candidates = [];
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            string normalized = NormalizeArchivePath(entry.FullName);
            if (string.IsNullOrEmpty(normalized) || IsJunkPath(normalized))
                continue;

            candidates.Add((entry, normalized));
        }

        string? worldRoot = candidates
            .Where(static item => string.Equals(Path.GetFileName(item.Path), "level.dat", StringComparison.OrdinalIgnoreCase))
            .Select(static item => Path.GetDirectoryName(item.Path)?.Replace('\\', '/') ?? string.Empty)
            .OrderBy(static path => path.Count(static ch => ch == '/'))
            .ThenBy(static path => path.Length)
            .FirstOrDefault();
        if (worldRoot is null)
            return [];

        string prefix = string.IsNullOrEmpty(worldRoot) ? string.Empty : worldRoot.TrimEnd('/') + "/";
        string worldName = string.IsNullOrEmpty(worldRoot)
            ? fallbackWorldName
            : worldRoot.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "world";

        List<ArchiveFile> files = [];
        foreach ((ZipArchiveEntry entry, string path) in candidates)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string relative = path[prefix.Length..];
            if (string.IsNullOrEmpty(relative) || IsJunkPath(relative))
                continue;
            files.Add(new ArchiveFile(entry, relative, worldName));
        }

        return files;
    }

    private static string NormalizeArchivePath(string value)
    {
        string normalized = value.Replace('\\', '/').TrimStart('/');
        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(static segment => segment is "." or ".."))
            throw new InvalidDataException("世界压缩包包含不安全的路径。");
        if (segments.Length > 0 && segments[0].Contains(':'))
            throw new InvalidDataException("世界压缩包包含绝对路径。");
        return string.Join('/', segments);
    }

    private static bool IsJunkPath(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(static segment => string.Equals(segment, "__MACOSX", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(segment, ".DS_Store", StringComparison.OrdinalIgnoreCase));

    private static string GetContainedPath(string root, string relativePath)
    {
        string rootWithSeparator = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!target.StartsWith(rootWithSeparator, comparison))
            throw new InvalidDataException("世界压缩包包含越界路径。");
        return target;
    }

    private static string GetUniqueDestination(string savesDirectory, string preferredName)
    {
        string candidate = Path.Combine(savesDirectory, preferredName);
        for (int suffix = 2; Directory.Exists(candidate) || File.Exists(candidate); suffix++)
            candidate = Path.Combine(savesDirectory, $"{preferredName} ({suffix})");
        return candidate;
    }

    private static string SanitizeDirectoryName(string value)
    {
        string name = string.Concat(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        return string.IsNullOrWhiteSpace(name) ? "world" : name;
    }

    private sealed record ArchiveFile(ZipArchiveEntry Entry, string RelativePath, string WorldName);
}
