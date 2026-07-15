// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Security.Cryptography;
using PCL.Application.Settings;
using PCL.Platform.Abstractions.Security;

namespace PCL.Application.Hosting.PluginPlatform;

/// <summary>Default host bridge: settings pages from <see cref="IPclHost"/> + immediate work queue.</summary>
internal sealed class PclPluginPlatformHost : IPclPluginPlatformHost
{
    public PclPluginPlatformHost(
        IHostSettingsPageGroupRegistry settingsPageGroups,
        IHostSettingsPageRegistry settingsPages,
        IPluginHostWorkQueue? workQueue = null,
        IPluginHostNotifications? notifications = null,
        IPluginHostInstanceQuery? instances = null,
        IPluginHostUiComposition? uiComposition = null,
        IPluginHostDeveloperDiagnostics? developerDiagnostics = null,
        IPluginHostNavigation? navigation = null,
        IPluginHostRawUiAccess? rawUiAccess = null,
        IPluginHostSecureStorage? secureStorage = null,
        IPluginHostUriLauncher? uriLauncher = null,
        string? applicationDataDirectory = null,
        string? cacheDirectory = null)
    {
        SettingsPageGroups = settingsPageGroups ?? throw new ArgumentNullException(nameof(settingsPageGroups));
        SettingsPages = settingsPages ?? throw new ArgumentNullException(nameof(settingsPages));
        WorkQueue = workQueue ?? ImmediatePluginHostWorkQueue.Instance;
        Notifications = notifications ?? CapturingPluginHostNotifications.Instance;
        DeveloperDiagnostics = developerDiagnostics ?? new InMemoryPluginHostDeveloperDiagnostics();
        SecureStorage = secureStorage ?? InMemoryPluginHostSecureStorage.Instance;
        UriLauncher = uriLauncher ?? UnavailablePluginHostUriLauncher.Instance;
        ApplicationDataDirectory = applicationDataDirectory ?? Path.GetTempPath();
        CacheDirectory = cacheDirectory ?? Path.GetTempPath();
        Instances = instances;
        UiComposition = uiComposition;
        Navigation = navigation;
        RawUiAccess = rawUiAccess;
    }

    public IHostSettingsPageGroupRegistry SettingsPageGroups { get; }

    public IHostSettingsPageRegistry SettingsPages { get; }

    public IPluginHostWorkQueue WorkQueue { get; }

    public IPluginHostNotifications Notifications { get; }

    public IPluginHostDeveloperDiagnostics DeveloperDiagnostics { get; }

    public IPluginHostSecureStorage SecureStorage { get; }

    public IPluginHostUriLauncher UriLauncher { get; }

    public string ApplicationDataDirectory { get; }

    public string CacheDirectory { get; }

    public IPluginHostInstanceQuery? Instances { get; }

    public IPluginHostUiComposition? UiComposition { get; }

    public IPluginHostNavigation? Navigation { get; }

    public IPluginHostRawUiAccess? RawUiAccess { get; }
}

internal sealed class InMemoryPluginHostDeveloperDiagnostics : IPluginHostDeveloperDiagnostics
{
    public bool IsEnabled { get; private set; }

    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}

internal sealed class InMemoryPluginHostSecureStorage : IPluginHostSecureStorage
{
    public static InMemoryPluginHostSecureStorage Instance { get; } = new();

    private readonly ConcurrentDictionary<string, byte[]> _values = new(StringComparer.Ordinal);

    public ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_values.TryGetValue(key, out byte[]? value)
            ? new SecureStorageReadResult(SecureStorageStatus.Success, value.ToArray())
            : new SecureStorageReadResult(SecureStorageStatus.NotFound));
    }

    public ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _values.AddOrUpdate(key, value.ToArray(), (_, previous) =>
        {
            CryptographicOperations.ZeroMemory(previous);
            return value.ToArray();
        });
        return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Success));
    }

    public ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_values.TryRemove(key, out byte[]? value))
            CryptographicOperations.ZeroMemory(value);
        return ValueTask.FromResult(new SecureStorageOperationResult(SecureStorageStatus.Success));
    }

    public ValueTask<SecureStorageReadResult> UnprotectLegacyWindowsAsync(
        ReadOnlyMemory<byte> encrypted,
        ReadOnlyMemory<byte> entropy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new SecureStorageReadResult(SecureStorageStatus.Unavailable));
    }
}

internal sealed class UnavailablePluginHostUriLauncher : IPluginHostUriLauncher
{
    public static UnavailablePluginHostUriLauncher Instance { get; } = new();

    public ValueTask<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}

internal sealed class ImmediatePluginHostWorkQueue : IPluginHostWorkQueue
{
    public static ImmediatePluginHostWorkQueue Instance { get; } = new();

    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(action());
    }
}

/// <summary>Captures notifications for diagnostics when no UI toast host is wired.</summary>
internal sealed class CapturingPluginHostNotifications : IPluginHostNotifications
{
    public static CapturingPluginHostNotifications Instance { get; } = new();

    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages.ToArray();

    public void ShowInformation(string message) =>
        _messages.Enqueue("[info] " + (message ?? string.Empty));

    public void ShowWarning(string message) =>
        _messages.Enqueue("[warn] " + (message ?? string.Empty));
}
