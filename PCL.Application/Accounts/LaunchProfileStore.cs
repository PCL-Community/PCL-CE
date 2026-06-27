// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace PCL.Application.Accounts;

public sealed class LaunchProfileStore : IDisposable
{
    private readonly SemaphoreSlim _accessLock = new(1, 1);

    public LaunchProfileStore(string profilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profilePath);
        ProfilePath = Path.GetFullPath(profilePath);
    }

    public string ProfilePath { get; }

    public async ValueTask<LaunchProfileLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(ProfilePath))
                return new(new LaunchProfileSet(), false, null);

            try
            {
                await using FileStream stream = new(
                    ProfilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 16 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                LaunchProfileSet? profiles = await JsonSerializer.DeserializeAsync(
                        stream,
                        LaunchProfileJsonContext.Default.LaunchProfileSet,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (profiles is null)
                    throw new InvalidDataException("The launch profile file is empty.");
                if (profiles.SchemaVersion is <= 0 or > LaunchProfileSet.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported launch profile schema: {profiles.SchemaVersion}.");
                }

                return new(profiles, false, null);
            }
            catch (Exception exception)
                when (exception is JsonException or InvalidDataException)
            {
                string backupPath = ProfilePath + ".invalid";
                File.Copy(ProfilePath, backupPath, overwrite: true);
                return new(new LaunchProfileSet(), true, backupPath);
            }
        }
        finally
        {
            _accessLock.Release();
        }
    }

    public async ValueTask SaveAsync(
        LaunchProfileSet profiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.SchemaVersion != LaunchProfileSet.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profiles),
                profiles.SchemaVersion,
                "Only the current launch profile schema can be saved.");
        }

        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            string directory = Path.GetDirectoryName(ProfilePath)
                ?? throw new InvalidOperationException(
                    "The launch profile path has no parent directory.");
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(ProfilePath)}.{Guid.NewGuid():N}.tmp");

            await using (FileStream stream = new(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 16 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                        stream,
                        profiles,
                        LaunchProfileJsonContext.Default.LaunchProfileSet,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, ProfilePath, overwrite: true);
            temporaryPath = null;
        }
        finally
        {
            if (temporaryPath is not null)
                File.Delete(temporaryPath);
            _accessLock.Release();
        }
    }

    public void Dispose() => _accessLock.Dispose();
}
