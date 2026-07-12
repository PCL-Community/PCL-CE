// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Hosting.PluginPlatform;

/// <summary>Installed third-party plugin row for host management UI (not public SDK ABI).</summary>
internal sealed record PluginCatalogEntry(
    string PluginId,
    string Name,
    string? ActiveVersion,
    string? InstalledPath,
    bool IsEnabled,
    bool IsLoaded,
    string? StatusMessage);

/// <summary>Privileged catalog / session surface used by Desktop settings (InternalsVisibleTo PCL.Plugin).</summary>
internal interface IPluginCatalogService
{
    string RootPath { get; }

    IReadOnlyList<PluginCatalogEntry> ListInstalled();

    Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default);

    Task<PluginCatalogEntry> InstallPackageAsync(string packagePath, CancellationToken cancellationToken = default);

    Task LoadEnabledAsync(CancellationToken cancellationToken = default);

    Task UnloadAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>Process-wide catalog access for Desktop + PCL.Plugin bootstrap.</summary>
internal static class PluginCatalogAccess
{
    private static IPluginCatalogService? _current;

    public static bool IsInitialized => _current is not null;

    public static IPluginCatalogService Current =>
        _current ?? throw new InvalidOperationException("Plugin catalog is not initialized.");

    public static void Initialize(IPluginCatalogService catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _current = catalog;
    }

    internal static void Reset() => _current = null;
}
