// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Collections.Concurrent;

namespace PCL.Application.Instances;

public static class InstanceMetadataStore
{
    public const string MetadataDirectoryName = "PCL";
    public const string MetadataFileName = "InstanceMetadata.json";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SaveLocks = new(StringComparer.Ordinal);

    public static string GetMetadataPath(string instanceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceDirectory);
        return Path.Combine(Path.GetFullPath(instanceDirectory), MetadataDirectoryName, MetadataFileName);
    }

    public static async Task<InstanceMetadata> LoadAsync(
        string instanceDirectory,
        CancellationToken cancellationToken = default)
    {
        string metadataPath = GetMetadataPath(instanceDirectory);
        if (!File.Exists(metadataPath))
            return new InstanceMetadata();

        try
        {
            await using FileStream stream = new(
                metadataPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 8 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            InstanceMetadata? metadata = await JsonSerializer.DeserializeAsync(
                    stream,
                    InstanceMetadataJsonContext.Default.InstanceMetadata,
                    cancellationToken)
                .ConfigureAwait(false);
            if (metadata is null)
                return new InstanceMetadata();

            if (metadata.SchemaVersion is <= 0 or > InstanceMetadata.CurrentSchemaVersion)
                return new InstanceMetadata();

            return metadata;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new InstanceMetadata();
        }
    }

    public static async Task SaveAsync(
        string instanceDirectory,
        InstanceMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        if (metadata.SchemaVersion != InstanceMetadata.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(metadata),
                metadata.SchemaVersion,
                "Only the current instance metadata schema can be saved.");
        }

        string metadataPath = GetMetadataPath(instanceDirectory);
        SemaphoreSlim saveLock = SaveLocks.GetOrAdd(metadataPath, static _ => new SemaphoreSlim(1, 1));
        await saveLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string temporaryPath = string.Empty;
        try
        {
            string directory = Path.GetDirectoryName(metadataPath)
                ?? throw new InvalidOperationException("The metadata path has no parent directory.");
            Directory.CreateDirectory(directory);

            temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(metadataPath)}.{Guid.NewGuid():N}.tmp");
            await using (FileStream stream = new(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 8 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        metadata,
                        InstanceMetadataJsonContext.Default.InstanceMetadata,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await MoveWithRetryAsync(temporaryPath, metadataPath, cancellationToken).ConfigureAwait(false);
            temporaryPath = string.Empty;
        }
        finally
        {
            if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
                TryDeleteTemporaryFile(temporaryPath);
            saveLock.Release();
        }
    }

    private static async Task MoveWithRetryAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 6;
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientFileAccess(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsTransientFileAccess(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (IsTransientFileAccess(ex))
        {
        }
    }

    public static async Task<InstanceMetadata> UpdateAsync(
        string instanceDirectory,
        Func<InstanceMetadata, InstanceMetadata> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        InstanceMetadata current = await LoadAsync(instanceDirectory, cancellationToken).ConfigureAwait(false);
        InstanceMetadata next = update(current);
        await SaveAsync(instanceDirectory, next, cancellationToken).ConfigureAwait(false);
        return next;
    }
}
