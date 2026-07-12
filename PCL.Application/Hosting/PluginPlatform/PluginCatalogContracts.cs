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
    string? StatusMessage,
    IReadOnlyList<string> RequiredDependencies,
    IReadOnlyList<string> MissingPrerequisites,
    string? DependencyState);

/// <summary>Local marketplace listing (directory-scanned <c>.pnp</c>, design §19 skeleton without Online).</summary>
internal sealed record PluginMarketListing(
    string PackagePath,
    string? PluginId,
    string? Name,
    string? Version,
    string? Summary,
    bool CanInspect,
    string? Error);

/// <summary>Host safety switches (design §17.4).</summary>
internal sealed record PluginSafetySettings(
    bool PluginSafeMode,
    bool UiSafeMode)
{
    public static PluginSafetySettings Default { get; } = new(false, false);
}

/// <summary>Result of applying a planned UI patch graph.</summary>
internal sealed record PluginUiPatchApplyResult(
    IReadOnlyList<string> AppliedGlobalIds,
    IReadOnlyList<string> SkippedGlobalIds,
    IReadOnlyList<string> BlockedBySafeMode,
    IReadOnlyList<string> BlockedByConflict,
    IReadOnlyList<string> VisuallyAppliedGlobalIds,
    bool UiSafeMode,
    IReadOnlyList<PluginUiConflictSummary> Conflicts);

/// <summary>Conflict row for host management UI (design §13.5).</summary>
internal sealed record PluginUiConflictSummary(
    string Kind,
    string Severity,
    string LeftGlobalId,
    string RightGlobalId,
    string Target,
    string Message,
    string? Resolution);

/// <summary>User resolution for a conflict pair (design §13.5).</summary>
internal enum PluginConflictResolution
{
    None = 0,
    DisableLeft = 1,
    DisableRight = 2,
    ForceBoth = 3
}

/// <summary>Local compatibility observation (design §19.3 skeleton, offline).</summary>
internal sealed record PluginCompatibilityRecord(
    string PluginA,
    string PluginB,
    string Target,
    string Result,
    string Evidence,
    DateTimeOffset ObservedAt);

/// <summary>Privileged catalog / session surface used by Desktop settings (InternalsVisibleTo PCL.Plugin).</summary>
internal interface IPluginCatalogService
{
    string RootPath { get; }

    PluginSafetySettings Safety { get; }

    void SetSafety(PluginSafetySettings settings);

    IReadOnlyList<PluginCatalogEntry> ListInstalled();

    Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default);

    Task<PluginCatalogEntry> InstallPackageAsync(string packagePath, CancellationToken cancellationToken = default);

    Task LoadEnabledAsync(CancellationToken cancellationToken = default);

    Task UnloadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Scan a local folder for <c>.pnp</c> packages (offline market source).</summary>
    IReadOnlyList<PluginMarketListing> BrowseLocalMarket(string directoryPath);

    /// <summary>Plan + apply UI patches under current safety policy.</summary>
    PluginUiPatchApplyResult ApplyUiPatches();

    IReadOnlyList<PluginUiConflictSummary> ListUiConflicts();

    void ResolveUiConflict(string leftGlobalId, string rightGlobalId, PluginConflictResolution resolution);

    IReadOnlyList<PluginCompatibilityRecord> ListCompatibility();
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
