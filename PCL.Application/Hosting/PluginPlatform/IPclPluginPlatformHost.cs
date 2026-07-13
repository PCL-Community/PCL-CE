// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Settings;

namespace PCL.Application.Hosting.PluginPlatform;

/// <summary>
/// Narrow internal bridge from PCL-N to the privileged <c>PCL.Plugin</c> platform.
/// Not part of the public third-party SDK ABI (design §3).
/// </summary>
internal interface IPclPluginPlatformHost
{
    IHostSettingsPageGroupRegistry SettingsPageGroups { get; }

    IHostSettingsPageRegistry SettingsPages { get; }

    IPluginHostWorkQueue WorkQueue { get; }

    IPluginHostNotifications Notifications { get; }

    /// <summary>Optional instance directory query for <c>pcl.instances.read</c>.</summary>
    IPluginHostInstanceQuery? Instances { get; }

    /// <summary>Optional Avalonia composition bridge for UI Patch apply (Desktop-only).</summary>
    IPluginHostUiComposition? UiComposition { get; }
}

/// <summary>
/// Host-side visual composition for plugin UI patches (no Avalonia types in Application ABI).
/// Desktop implements with real controls; PCL.Plugin only calls these methods.
/// </summary>
internal interface IPluginHostUiComposition
{
    void ClearSlot(string surfaceId, string slotId);

    void Inject(string surfaceId, string slotId, HostUiInjectionRequest request);

    bool TrySetProperty(string surfaceId, string? slotId, string propertyPath, string? value);

    bool TrySetVisible(string surfaceId, bool isVisible);

    bool IsTargetRegistered(string surfaceId);

    /// <summary>Wraps a registered surface target in a host-owned decorator (design §12.4 Wrap).</summary>
    bool TryWrap(string surfaceId, HostUiWrapRequest request);

    /// <summary>
    /// Replaces a registered surface target with a host-owned placeholder control
    /// (design §12.4 / §13.3 Replace — exclusive visual placeholder).
    /// </summary>
    bool TryReplace(string surfaceId, HostUiReplaceRequest request);

    /// <summary>Restores targets to pre-wrap/replace state before a new apply pass.</summary>
    void ResetWrapAndReplace(string surfaceId);
}

internal sealed record HostUiInjectionRequest(
    string PluginId,
    string ContributionId,
    string Title,
    int Order);

internal sealed record HostUiWrapRequest(
    string PluginId,
    string OperationId,
    string? Label,
    int Order);

internal sealed record HostUiReplaceRequest(
    string PluginId,
    string OperationId,
    string? Title);

/// <summary>Read-only Minecraft instance listing for plugins (design §9).</summary>
internal interface IPluginHostInstanceQuery
{
    IReadOnlyList<HostPluginInstanceInfo> ListInstances();
}

internal sealed record HostPluginInstanceInfo(
    string Id,
    string Name,
    string InstanceDirectory,
    string? VersionJsonPath);

/// <summary>Host-owned serialized work queue; Desktop supplies its UI-thread implementation.</summary>
internal interface IPluginHostWorkQueue
{
    void Post(Action action);

    Task InvokeAsync(Action action, CancellationToken cancellationToken = default);

    Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default);
}

internal interface IPluginHostNotifications
{
    void ShowInformation(string message);

    void ShowWarning(string message);
}

/// <summary>Process-wide access for <c>PCL.Plugin</c> (InternalsVisibleTo).</summary>
internal static class PluginPlatformHostAccess
{
    private static IPclPluginPlatformHost? _current;

    public static bool IsInitialized => _current is not null;

    public static IPclPluginPlatformHost Current =>
        _current ?? throw new InvalidOperationException("Plugin platform host bridge is not initialized.");

    public static void Initialize(IPclPluginPlatformHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _current = host;
    }

    /// <summary>Test-only reset.</summary>
    internal static void Reset() => _current = null;
}
