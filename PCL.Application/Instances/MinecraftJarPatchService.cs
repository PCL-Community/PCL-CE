// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;

namespace PCL.Application.Instances;

public sealed record MinecraftJarPatchRequest
{
    public required string TargetJarPath { get; init; }

    public required string PatchArchivePath { get; init; }
}

public static class MinecraftJarPatchService
{
    public static async Task<int> ApplyAsync(
        MinecraftJarPatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetJarPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PatchArchivePath);

        if (!File.Exists(request.TargetJarPath))
            throw new FileNotFoundException("目标核心文件不存在。", request.TargetJarPath);
        if (!File.Exists(request.PatchArchivePath))
            throw new FileNotFoundException("补丁压缩包不存在。", request.PatchArchivePath);

        int patched = 0;
        using ZipArchive target = ZipFile.Open(request.TargetJarPath, ZipArchiveMode.Update);
        using ZipArchive patch = ZipFile.OpenRead(request.PatchArchivePath);
        foreach (ZipArchiveEntry sourceEntry in patch.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(sourceEntry.Name))
                continue;
            if (sourceEntry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                continue;

            ZipArchiveEntry? existing = target.GetEntry(sourceEntry.FullName);
            existing?.Delete();
            ZipArchiveEntry targetEntry = target.CreateEntry(sourceEntry.FullName, CompressionLevel.Optimal);
            await using Stream source = sourceEntry.Open();
            await using Stream destination = targetEntry.Open();
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            patched++;
        }

        return patched;
    }
}
