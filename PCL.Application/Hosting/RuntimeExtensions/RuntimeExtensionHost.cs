using System.Collections.Concurrent;
using System.Security.Cryptography;
using PCL.Application.Accounts;
using PCL.Application.Downloads;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.Platform.Abstractions.Processes;
using PCL.Platform.Abstractions.Security;

namespace PCL.Application.Hosting.RuntimeExtensions;

internal sealed class RuntimeExtensionHost : IRuntimeExtensionHost
{
    public RuntimeExtensionHost(
        IHostSettingsPageGroupRegistry settingsPageGroups,
        IHostSettingsPageRegistry settingsPages,
        IHostWorkQueue? workQueue = null,
        IHostNotifications? notifications = null,
        IHostInstanceQuery? instances = null,
        IHostUiComposition? uiComposition = null,
        IHostDeveloperDiagnostics? developerDiagnostics = null,
        IHostDynamicNavigation? navigation = null,
        IHostRawUiAccess? rawUiAccess = null,
        IHostSecureStorage? secureStorage = null,
        IHostUriLauncher? uriLauncher = null,
        string? applicationDataDirectory = null,
        string? cacheDirectory = null,
        IGameSessionRegistry? gameSessions = null,
        IProcessService? processes = null,
        IHostClipboard? clipboard = null,
        IAccountProviderRegistry? accounts = null,
        IDownloadSourceRegistry? downloads = null,
        ILaunchPipelineBuilder? launching = null)
    {
        SettingsPageGroups = settingsPageGroups ?? throw new ArgumentNullException(nameof(settingsPageGroups));
        SettingsPages = settingsPages ?? throw new ArgumentNullException(nameof(settingsPages));
        WorkQueue = workQueue ?? ImmediateHostWorkQueue.Instance;
        Notifications = notifications ?? CapturingHostNotifications.Instance;
        DeveloperDiagnostics = developerDiagnostics ?? new InMemoryHostDeveloperDiagnostics();
        SecureStorage = secureStorage ?? InMemoryHostSecureStorage.Instance;
        UriLauncher = uriLauncher ?? UnavailableHostUriLauncher.Instance;
        Processes = processes;
        Clipboard = clipboard;
        Accounts = accounts ?? new AccountProviderRegistry();
        Downloads = downloads ?? new DownloadSourceRegistry();
        Launching = launching ?? new LaunchPipelineBuilder();
        ApplicationDataDirectory = applicationDataDirectory ?? Path.GetTempPath();
        CacheDirectory = cacheDirectory ?? Path.GetTempPath();
        Instances = instances;
        GameSessions = gameSessions ?? GameSessionRegistry.Shared;
        UiComposition = uiComposition;
        Navigation = navigation;
        RawUiAccess = rawUiAccess;
    }

    public IHostSettingsPageGroupRegistry SettingsPageGroups { get; }
    public IHostSettingsPageRegistry SettingsPages { get; }
    public IHostWorkQueue WorkQueue { get; }
    public IHostNotifications Notifications { get; }
    public IHostDeveloperDiagnostics DeveloperDiagnostics { get; }
    public IHostSecureStorage SecureStorage { get; }
    public IHostUriLauncher UriLauncher { get; }
    public IProcessService? Processes { get; }
    public IHostClipboard? Clipboard { get; }
    public IAccountProviderRegistry Accounts { get; }
    public IDownloadSourceRegistry Downloads { get; }
    public ILaunchPipelineBuilder Launching { get; }
    public string ApplicationDataDirectory { get; }
    public string CacheDirectory { get; }
    public IHostInstanceQuery? Instances { get; }
    public IGameSessionRegistry GameSessions { get; }
    public IHostUiComposition? UiComposition { get; }
    public IHostDynamicNavigation? Navigation { get; }
    public IHostRawUiAccess? RawUiAccess { get; }
}

internal sealed class InMemoryHostDeveloperDiagnostics : IHostDeveloperDiagnostics
{
    public bool IsEnabled { get; private set; }
    public void SetEnabled(bool enabled) => IsEnabled = enabled;
}

internal sealed class InMemoryHostSecureStorage : IHostSecureStorage
{
    public static InMemoryHostSecureStorage Instance { get; } = new();
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

internal sealed class UnavailableHostUriLauncher : IHostUriLauncher
{
    public static UnavailableHostUriLauncher Instance { get; } = new();
    public ValueTask<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }
}

internal sealed class ImmediateHostWorkQueue : IHostWorkQueue
{
    public static ImmediateHostWorkQueue Instance { get; } = new();
    public void Post(Action action) { ArgumentNullException.ThrowIfNull(action); action(); }
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

internal sealed class CapturingHostNotifications : IHostNotifications
{
    public static CapturingHostNotifications Instance { get; } = new();
    private readonly ConcurrentQueue<string> _messages = new();
    public IReadOnlyCollection<string> Messages => _messages.ToArray();
    public void ShowInformation(string message) => _messages.Enqueue("[info] " + (message ?? string.Empty));
    public void ShowWarning(string message) => _messages.Enqueue("[warn] " + (message ?? string.Empty));
}
