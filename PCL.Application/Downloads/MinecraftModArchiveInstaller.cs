// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using System.Text.Json;

namespace PCL.Application.Downloads;

public static class MinecraftModArchiveInstaller
{
    public static string Install(string downloadedArchivePath, string modsDirectory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadedArchivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(modsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!File.Exists(downloadedArchivePath))
            throw new FileNotFoundException("待安装的模组文件不存在。", downloadedArchivePath);

        Directory.CreateDirectory(modsDirectory);
        string finalPath = Path.Combine(modsDirectory, Path.GetFileName(fileName));
        List<(string Original, string Disabled)> moved = [];
        try
        {
            foreach (string conflict in FindConflicts(downloadedArchivePath, modsDirectory, finalPath))
            {
                string disabled = CreateDisabledPath(conflict);
                File.Move(conflict, disabled);
                moved.Add((conflict, disabled));
            }

            File.Move(downloadedArchivePath, finalPath, overwrite: false);
            return finalPath;
        }
        catch
        {
            for (int index = moved.Count - 1; index >= 0; index--)
            {
                (string original, string disabled) = moved[index];
                if (File.Exists(disabled) && !File.Exists(original))
                    File.Move(disabled, original);
            }

            throw;
        }
    }

    public static IReadOnlyList<string> DisableConflicts(string installedArchivePath, string modsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installedArchivePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(modsDirectory);
        if (!File.Exists(installedArchivePath) || !Directory.Exists(modsDirectory))
            return [];

        List<string> disabledPaths = [];
        foreach (string conflict in FindConflicts(installedArchivePath, modsDirectory, installedArchivePath))
        {
            string disabled = CreateDisabledPath(conflict);
            File.Move(conflict, disabled);
            disabledPaths.Add(disabled);
        }

        return disabledPaths;
    }

    private static IEnumerable<string> FindConflicts(
        string incomingArchivePath,
        string modsDirectory,
        string finalPath)
    {
        HashSet<string> incomingIds = ReadFabricModIds(incomingArchivePath);
        foreach (string existing in Directory.EnumerateFiles(modsDirectory, "*.jar", SearchOption.TopDirectoryOnly))
        {
            if (string.Equals(existing, incomingArchivePath, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(existing, finalPath, StringComparison.OrdinalIgnoreCase))
            {
                yield return existing;
                continue;
            }

            if (incomingIds.Count == 0)
                continue;

            HashSet<string> existingIds = ReadFabricModIds(existing);
            if (existingIds.Overlaps(incomingIds))
                yield return existing;
        }
    }

    private static HashSet<string> ReadFabricModIds(string archivePath)
    {
        HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            using ZipArchive archive = ZipFile.OpenRead(archivePath);
            ZipArchiveEntry? metadata = archive.GetEntry("fabric.mod.json");
            if (metadata is null)
                return result;

            using Stream stream = metadata.Open();
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(id.GetString()))
            {
                result.Add(id.GetString()!);
            }

            if (root.TryGetProperty("provides", out JsonElement provides) &&
                provides.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement provided in provides.EnumerateArray())
                {
                    if (provided.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(provided.GetString()))
                    {
                        result.Add(provided.GetString()!);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException or UnauthorizedAccessException)
        {
            // Unknown archives still install normally; only same-name replacement applies.
        }

        return result;
    }

    private static string CreateDisabledPath(string path)
    {
        string candidate = path + ".disabled";
        for (int index = 2; File.Exists(candidate); index++)
            candidate = path + "." + index + ".disabled";
        return candidate;
    }
}
