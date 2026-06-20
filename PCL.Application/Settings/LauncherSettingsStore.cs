// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace PCL.Application.Settings;

public sealed class LauncherSettingsStore : IDisposable
{
    private readonly SemaphoreSlim _accessLock = new(1, 1);

    public LauncherSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        SettingsPath = Path.GetFullPath(settingsPath);
    }

    public string SettingsPath { get; }

    public async ValueTask<LauncherSettingsLoadResult> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(SettingsPath))
                return new(new LauncherSettings(), false, null);

            try
            {
                await using FileStream stream = new(
                    SettingsPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 16 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                LauncherSettings? settings = await JsonSerializer.DeserializeAsync(
                        stream,
                        LauncherSettingsJsonContext.Default.LauncherSettings,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (settings is null)
                    throw new InvalidDataException("The launcher settings file is empty.");
                if (settings.SchemaVersion is <= 0 or > LauncherSettings.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"Unsupported launcher settings schema: {settings.SchemaVersion}.");
                }

                return new(settings, false, null);
            }
            catch (Exception exception)
                when (exception is JsonException or InvalidDataException)
            {
                string backupPath = SettingsPath + ".invalid";
                File.Copy(SettingsPath, backupPath, overwrite: true);
                return new(new LauncherSettings(), true, backupPath);
            }
        }
        finally
        {
            _accessLock.Release();
        }
    }

    public async ValueTask SaveAsync(
        LauncherSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.SchemaVersion != LauncherSettings.CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.SchemaVersion,
                "Only the current launcher settings schema can be saved.");
        }

        await _accessLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            string directory = Path.GetDirectoryName(SettingsPath)
                ?? throw new InvalidOperationException(
                    "The launcher settings path has no parent directory.");
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(SettingsPath)}.{Guid.NewGuid():N}.tmp");

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
                        settings,
                        LauncherSettingsJsonContext.Default.LauncherSettings,
                        cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, SettingsPath, overwrite: true);
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
