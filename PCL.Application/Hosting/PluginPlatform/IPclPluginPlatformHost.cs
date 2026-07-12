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
    IHostSettingsPageRegistry SettingsPages { get; }

    IPluginHostDispatcher Dispatcher { get; }

    IPluginHostNotifications Notifications { get; }

    /// <summary>Optional instance directory query for <c>pcl.instances.read</c>.</summary>
    IPluginHostInstanceQuery? Instances { get; }
}

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

internal interface IPluginHostDispatcher
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
