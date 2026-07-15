// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Hosting.PluginPlatform;

internal static class PluginSettingsPageIds
{
    public const string Group = "pcl.plugin";
    public const string OnlineGroup = "pcl.online";
    public const string Account = "pcl.online.account";
    public const string OnlineData = "pcl.online.data";
    public const string LegacySettings = "pcl.plugin.settings";
    public const string Installed = "pcl.plugin.installed";
    public const string Market = "pcl.plugin.market";
    public const string Developer = "pcl.plugin.developer";
    public const string Safety = "pcl.plugin.safety";
    public const string UiPatches = "pcl.plugin.ui-patches";
    public const string Compatibility = "pcl.plugin.compatibility";
}

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
    string? DependencyState,
    IReadOnlyList<PluginPermissionRequest>? PermissionRequests = null,
    bool RequiresPermissionApproval = false,
    bool CanRollback = false,
    string? RollbackVersion = null);

internal sealed record PluginPermissionRequest(string Id, string Reason, bool IsHighRisk);

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
    bool UiSafeMode,
    bool DeveloperMode = false,
    bool AllowUnsignedPlugins = false,
    bool ShowSafetyPage = false,
    bool ShowUiPatchesPage = false,
    bool ShowCompatibilityPage = false,
    bool IsolationMode = false,
    IReadOnlyList<string>? IsolationPluginIds = null)
{
    public static PluginSafetySettings Default { get; } = new(
        false, false, false, false, false, false, false, false, []);
}

/// <summary>Current first-party developer entitlement state. Order identifiers are never exposed to the host.</summary>
internal sealed record PluginDeveloperAccessState(
    bool IsVerified,
    bool IsPermanent,
    string? ShowAmount,
    DateTimeOffset? SponsoredAt,
    DateTimeOffset? ExpiresAt,
    string StatusMessage)
{
    public static PluginDeveloperAccessState None { get; } = new(
        false,
        false,
        null,
        null,
        null,
        "尚未验证爱发电订单。");
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

/// <summary>Result of creating a redacted plugin diagnostic package (design §20.4).</summary>
internal sealed record PluginDiagnosticsExport(
    string PackagePath,
    string CompositionHash,
    DateTimeOffset CreatedAt,
    long SizeBytes);

/// <summary>Privileged catalog / session surface used by Desktop settings (InternalsVisibleTo PCL.Plugin).</summary>
internal interface IPluginCatalogService
{
    string RootPath { get; }

    PluginSafetySettings Safety { get; }

    PluginDeveloperAccessState DeveloperAccess { get; }

    void SetSafety(PluginSafetySettings settings);

    Task<PluginDeveloperAccessState> VerifyDeveloperAccessAsync(
        string orderNumber,
        CancellationToken cancellationToken = default);

    IReadOnlyList<PluginCatalogEntry> ListInstalled();

    Task SetEnabledAsync(string pluginId, bool enabled, CancellationToken cancellationToken = default);

    Task ApprovePermissionsAsync(string pluginId, CancellationToken cancellationToken = default);

    Task RollbackAsync(string pluginId, CancellationToken cancellationToken = default);

    Task<PluginCatalogEntry> InstallPackageAsync(string packagePath, CancellationToken cancellationToken = default);

    Task LoadEnabledAsync(CancellationToken cancellationToken = default);

    Task UnloadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Scan a local folder for <c>.pnp</c> packages (offline market source).</summary>
    IReadOnlyList<PluginMarketListing> BrowseLocalMarket(string directoryPath);

    /// <summary>
    /// Whether a remote market client is configured. HTTP market ABI lives in
    /// <c>PCL.N.Plugin.IPluginMarketClient</c> (SDK); server is not shipped yet.
    /// </summary>
    bool IsRemoteMarketConfigured { get; }

    /// <summary>Plan + apply UI patches under current safety policy.</summary>
    PluginUiPatchApplyResult ApplyUiPatches();

    IReadOnlyList<PluginUiConflictSummary> ListUiConflicts();

    void ResolveUiConflict(string leftGlobalId, string rightGlobalId, PluginConflictResolution resolution);

    IReadOnlyList<PluginCompatibilityRecord> ListCompatibility();

    /// <summary>Writes a redacted ZIP package for support and compatibility diagnosis.</summary>
    Task<PluginDiagnosticsExport> ExportDiagnosticsAsync(
        string packagePath,
        CancellationToken cancellationToken = default);
}

/// <summary>Process-wide catalog access for Desktop + PCL.Plugin bootstrap.</summary>
internal static class PluginCatalogAccess
{
    private static IPluginCatalogService? _current;

    public static event Action? SafetyChanged;

    public static bool IsInitialized => _current is not null;

    public static IPluginCatalogService Current =>
        _current ?? throw new InvalidOperationException("Plugin catalog is not initialized.");

    public static void Initialize(IPluginCatalogService catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _current = catalog;
    }

    public static void NotifySafetyChanged() => SafetyChanged?.Invoke();

    internal static void Reset()
    {
        _current = null;
        SafetyChanged = null;
    }
}
