// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions;

public enum PclPluginKind
{
    BuiltIn,
    External
}

public enum PclPluginFeatureKind
{
    MainNavigationPage,
    SettingsPage,
    AccountProvider,
    OnlineService,
    DownloadSource,
    LaunchHook
}

public sealed record PclPluginDescriptor(
    string Id,
    string DisplayName,
    Version Version,
    PclPluginKind Kind,
    string Description);

public sealed record PclPluginFeatureDescriptor(
    string PluginId,
    string FeatureId,
    string DisplayName,
    PclPluginFeatureKind Kind);

public interface IPclPluginModule
{
    PclPluginDescriptor Descriptor { get; }

    void Register(IPluginRegistrationContext context);
}

public interface IPclPluginHost
{
    Version HostVersion { get; }

    IReadOnlyList<PclPluginDescriptor> Plugins { get; }

    IReadOnlyList<PclPluginFeatureDescriptor> Features { get; }

    void RegisterModule(IPclPluginModule pluginModule);
}

public interface IPluginRegistrationContext
{
    Version HostVersion { get; }

    void RegisterFeature(PclPluginFeatureDescriptor feature);
}
