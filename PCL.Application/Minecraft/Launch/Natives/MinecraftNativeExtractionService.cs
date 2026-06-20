// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;

namespace PCL.Application.Minecraft.Launch.Natives;

public static class MinecraftNativeExtractionService
{
    public static MinecraftNativeExtractionResult Extract(MinecraftNativeExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetDirectory);

        string targetRoot = Path.GetFullPath(request.TargetDirectory);
        Directory.CreateDirectory(targetRoot);
        string targetRootWithSeparator = EnsureTrailingSeparator(targetRoot);
        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        HashSet<string> expectedFiles = new(pathComparer);
        List<string> extractedFiles = [];
        List<string> upToDateFiles = [];
        List<string> deletedFiles = [];
        List<string> lockedFiles = [];

        foreach (string archivePath in request.ArchivePaths)
        {
            if (string.IsNullOrWhiteSpace(archivePath))
                continue;

            using ZipArchive archive = OpenArchive(archivePath);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name) ||
                    !IsNativeEntryForPlatform(entry.FullName, request.OperatingSystem))
                {
                    continue;
                }

                string targetPath = GetSafeEntryTargetPath(targetRootWithSeparator, entry.FullName);
                expectedFiles.Add(targetPath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                if (File.Exists(targetPath))
                {
                    FileInfo existingFile = new(targetPath);
                    if (existingFile.Length == entry.Length)
                    {
                        upToDateFiles.Add(targetPath);
                        continue;
                    }

                    if (!TryDelete(targetPath, lockedFiles))
                        continue;
                }

                using Stream input = entry.Open();
                using FileStream output = File.Create(targetPath);
                input.CopyTo(output);
                extractedFiles.Add(targetPath);
            }
        }

        if (request.DeleteUnknownFiles)
            DeleteUnknownFiles(targetRoot, expectedFiles, deletedFiles, lockedFiles);

        return new MinecraftNativeExtractionResult(
            extractedFiles,
            upToDateFiles,
            deletedFiles,
            lockedFiles);
    }

    private static ZipArchive OpenArchive(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException ex)
        {
            throw new MinecraftNativeArchiveException(archivePath, ex);
        }
    }

    private static void DeleteUnknownFiles(
        string targetRoot,
        HashSet<string> expectedFiles,
        List<string> deletedFiles,
        List<string> lockedFiles)
    {
        string targetRootWithSeparator = EnsureTrailingSeparator(targetRoot);
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (string file in Directory.EnumerateFiles(targetRoot, "*", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(file);
            if (expectedFiles.Contains(fullPath))
                continue;

            if (TryDelete(fullPath, lockedFiles))
                deletedFiles.Add(fullPath);
        }

        foreach (string directory in Directory.EnumerateDirectories(targetRoot, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static path => path.Length))
        {
            if (Directory.EnumerateFileSystemEntries(directory).Any())
                continue;

            string fullPath = Path.GetFullPath(directory);
            if (!fullPath.StartsWith(targetRootWithSeparator, comparison))
                continue;

            Directory.Delete(fullPath);
        }
    }

    private static bool TryDelete(string path, List<string> lockedFiles)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            lockedFiles.Add(path);
            return false;
        }
        catch (IOException)
        {
            lockedFiles.Add(path);
            return false;
        }
    }

    private static string GetSafeEntryTargetPath(string targetRootWithSeparator, string entryName)
    {
        string relativePath = entryName.Replace('/', Path.DirectorySeparatorChar);
        string targetPath = Path.GetFullPath(Path.Combine(targetRootWithSeparator, relativePath));
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!targetPath.StartsWith(targetRootWithSeparator, comparison))
            throw new InvalidOperationException($"Native archive entry escapes target directory: {entryName}");

        return targetPath;
    }

    private static bool IsNativeEntryForPlatform(string entryName, MinecraftNativeOperatingSystem operatingSystem)
    {
        string fileName = Path.GetFileName(entryName);
        return operatingSystem switch
        {
            MinecraftNativeOperatingSystem.Win32 => fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase),
            MinecraftNativeOperatingSystem.Linux =>
                fileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                fileName.Contains(".so.", StringComparison.OrdinalIgnoreCase),
            MinecraftNativeOperatingSystem.MacOs =>
                fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".jnilib", StringComparison.OrdinalIgnoreCase),
            _ => IsKnownNativeExtension(fileName)
        };
    }

    private static bool IsKnownNativeExtension(string fileName) =>
        fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
        fileName.Contains(".so.", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".jnilib", StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path)
    {
        string fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }
}
