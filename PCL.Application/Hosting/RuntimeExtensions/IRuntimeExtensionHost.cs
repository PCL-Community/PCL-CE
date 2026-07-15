using PCL.Application.Accounts;
using PCL.Application.Downloads;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.Platform.Abstractions.Processes;
using PCL.Platform.Abstractions.Security;

namespace PCL.Application.Hosting.RuntimeExtensions;

internal interface IRuntimeExtensionHost
{
    IHostSettingsPageGroupRegistry SettingsPageGroups { get; }
    IHostSettingsPageRegistry SettingsPages { get; }
    IHostWorkQueue WorkQueue { get; }
    IHostNotifications Notifications { get; }
    IHostDeveloperDiagnostics DeveloperDiagnostics { get; }
    IHostSecureStorage SecureStorage { get; }
    IHostUriLauncher UriLauncher { get; }
    IProcessService? Processes { get; }
    IHostClipboard? Clipboard { get; }
    IAccountProviderRegistry Accounts { get; }
    IDownloadSourceRegistry Downloads { get; }
    ILaunchPipelineBuilder Launching { get; }
    string ApplicationDataDirectory { get; }
    string CacheDirectory { get; }
    IHostInstanceQuery? Instances { get; }
    IGameSessionRegistry GameSessions { get; }
    IHostUiComposition? UiComposition { get; }
    IHostDynamicNavigation? Navigation { get; }
    IHostRawUiAccess? RawUiAccess { get; }
}

internal interface IHostUiComposition
{
    IHostUiMutationTransaction BeginTransaction(IReadOnlyCollection<string> surfaceIds) => NoopHostUiMutationTransaction.Instance;
    void ClearSlot(string surfaceId, string slotId);
    bool Inject(string surfaceId, string slotId, HostUiInjectionRequest request);
    bool TrySetProperty(string surfaceId, string? slotId, string propertyPath, string? value);
    bool TrySetVisible(string surfaceId, bool isVisible);
    bool IsTargetRegistered(string surfaceId);
    bool TryWrap(string surfaceId, HostUiWrapRequest request);
    bool TryReplace(string surfaceId, HostUiReplaceRequest request);
    bool TryReorder(string surfaceId, string? slotId, int order) => false;
    bool TrySetResource(string surfaceId, string key, object? value) => false;
    bool TrySetStyle(string surfaceId, string selector, object? value) => false;
    bool TrySetTemplate(string surfaceId, object? value) => false;
    bool TryInterceptInput(string surfaceId, string operationId) => false;
    void ResetWrapAndReplace(string surfaceId);
    object? ResolveTarget(string surfaceId) => null;
    long GetTargetGeneration(string surfaceId) => 0;
}

internal interface IHostUiMutationTransaction : IDisposable
{
    void Commit();
}

internal sealed class NoopHostUiMutationTransaction : IHostUiMutationTransaction
{
    public static NoopHostUiMutationTransaction Instance { get; } = new();
    public void Commit() { }
    public void Dispose() { }
}

internal interface IHostDynamicNavigation
{
    IHostRegistration RegisterPage(HostPageRegistration registration);
    Task NavigateAsync(string route, CancellationToken cancellationToken = default);
}

internal sealed record HostPageRegistration(
    string OwnerId,
    string OperationId,
    string Route,
    string Title,
    string? Icon,
    int Order,
    Func<object> CreatePage);

internal interface IHostRegistration : IAsyncDisposable
{
    string Id { get; }
    bool IsActive { get; }
}

internal interface IHostRawUiAccess
{
    object Application { get; }
    IReadOnlyList<object> TopLevels { get; }
    object? ResolveTarget(string surfaceId);
    long GetTargetGeneration(string surfaceId);
}

internal sealed record HostUiInjectionRequest(
    string OwnerId,
    string ContributionId,
    string Title,
    int Order,
    Func<object>? CreateContent = null);

internal sealed record HostUiWrapRequest(string OwnerId, string OperationId, string? Label, int Order);
internal sealed record HostUiReplaceRequest(string OwnerId, string OperationId, string? Title);

internal interface IHostInstanceQuery
{
    IReadOnlyList<HostInstanceInfo> ListInstances();
}

internal sealed record HostInstanceInfo(string Id, string Name, string InstanceDirectory, string? VersionJsonPath);

internal interface IHostWorkQueue
{
    void Post(Action action);
    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
    Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default);
}

internal interface IHostNotifications
{
    void ShowInformation(string message);
    void ShowWarning(string message);
}

internal interface IHostDeveloperDiagnostics
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

internal interface IHostSecureStorage
{
    ValueTask<SecureStorageReadResult> ReadAsync(string key, CancellationToken cancellationToken = default);
    ValueTask<SecureStorageOperationResult> WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);
    ValueTask<SecureStorageOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default);
    ValueTask<SecureStorageReadResult> UnprotectLegacyWindowsAsync(
        ReadOnlyMemory<byte> encrypted,
        ReadOnlyMemory<byte> entropy,
        CancellationToken cancellationToken = default);
}

internal interface IHostClipboard
{
    ValueTask<string?> ReadTextAsync(CancellationToken cancellationToken = default);
    ValueTask WriteTextAsync(string text, CancellationToken cancellationToken = default);
}

internal interface IHostUriLauncher
{
    ValueTask<bool> OpenAsync(Uri uri, CancellationToken cancellationToken = default);
}

internal static class RuntimeExtensionHostAccess
{
    private static IRuntimeExtensionHost? _current;

    public static bool IsInitialized => _current is not null;
    public static IRuntimeExtensionHost Current => _current ?? Fallback;

    private static IRuntimeExtensionHost Fallback { get; } = new RuntimeExtensionHost(
        new HostSettingsPageGroupRegistry(),
        new HostSettingsPageRegistry());

    public static void Initialize(IRuntimeExtensionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _current = host;
    }

    internal static void Reset() => _current = null;
}
