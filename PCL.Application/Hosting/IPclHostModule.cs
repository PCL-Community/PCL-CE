// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Extensions;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.Application.Accounts;
using PCL.Application.Downloads;
using PCL.UI.Abstractions.Commands;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Themes;

namespace PCL.Application.Hosting;

public interface IPclHostModule
{
    HostModuleId Id { get; }

    void Configure(IPclHostBuilder builder);
}

public interface IPclHostBuilder
{
    IServiceRegistry Services { get; }

    IExtensionRegistry Extensions { get; }

    INavigationRegistry Navigation { get; }

    ICommandRegistry Commands { get; }

    ISettingsRegistry Settings { get; }

    IThemeRegistry Themes { get; }

    IAccountProviderRegistry Accounts { get; }

    IDownloadSourceRegistry Downloads { get; }

    ILaunchPipelineBuilder Launching { get; }
}

public interface IPclHost
{
    IServiceProvider Services { get; }

    IExtensionRegistry Extensions { get; }

    INavigationRegistry Navigation { get; }

    ICommandRegistry Commands { get; }

    ISettingsRegistry Settings { get; }

    IThemeRegistry Themes { get; }

    IAccountProviderRegistry Accounts { get; }

    IDownloadSourceRegistry Downloads { get; }

    ILaunchPipelineBuilder Launching { get; }

    IReadOnlyList<HostModuleId> ModuleIds { get; }
}

public sealed class PclHostBuilder : IPclHostBuilder
{
    private readonly List<HostModuleId> _moduleIds = [];
    private readonly HashSet<string> _moduleIdSet = new(StringComparer.OrdinalIgnoreCase);

    public IServiceRegistry Services { get; } = new ServiceRegistry();

    public IExtensionRegistry Extensions { get; } = new ExtensionRegistry();

    public INavigationRegistry Navigation { get; } = new NavigationRegistry();

    public ICommandRegistry Commands { get; } = new CommandRegistry();

    public ISettingsRegistry Settings { get; } = new SettingsRegistry();

    public IThemeRegistry Themes { get; } = new ThemeRegistry();

    public IAccountProviderRegistry Accounts { get; } = new AccountProviderRegistry();

    public IDownloadSourceRegistry Downloads { get; } = new DownloadSourceRegistry();

    public ILaunchPipelineBuilder Launching { get; } = new LaunchPipelineBuilder();

    public PclHostBuilder AddModule(HostModuleId id, Action<IPclHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (string.IsNullOrWhiteSpace(id.Value))
            throw new ArgumentException("Host Module ID 不能为空。", nameof(id));
        if (_moduleIdSet.Contains(id.Value))
            throw new InvalidOperationException($"Host Module 已注册：{id.Value}");

        configure(this);
        _moduleIdSet.Add(id.Value);
        _moduleIds.Add(id);
        return this;
    }

    public PclHostBuilder AddModule(IPclHostModule hostModule)
    {
        ArgumentNullException.ThrowIfNull(hostModule);
        return AddModule(hostModule.Id, hostModule.Configure);
    }

    public IPclHost Build() =>
        new PclHost(
            Services,
            Extensions,
            Navigation,
            Commands,
            Settings,
            Themes,
            Accounts,
            Downloads,
            Launching,
            _moduleIds.ToArray());
}

internal sealed class PclHost(
    IServiceProvider services,
    IExtensionRegistry extensions,
    INavigationRegistry navigation,
    ICommandRegistry commands,
    ISettingsRegistry settings,
    IThemeRegistry themes,
    IAccountProviderRegistry accounts,
    IDownloadSourceRegistry downloads,
    ILaunchPipelineBuilder launching,
    IReadOnlyList<HostModuleId> moduleIds) : IPclHost
{
    public IServiceProvider Services { get; } = services;

    public IExtensionRegistry Extensions { get; } = extensions;

    public INavigationRegistry Navigation { get; } = navigation;

    public ICommandRegistry Commands { get; } = commands;

    public ISettingsRegistry Settings { get; } = settings;

    public IThemeRegistry Themes { get; } = themes;

    public IAccountProviderRegistry Accounts { get; } = accounts;

    public IDownloadSourceRegistry Downloads { get; } = downloads;

    public ILaunchPipelineBuilder Launching { get; } = launching;

    public IReadOnlyList<HostModuleId> ModuleIds { get; } = moduleIds;
}
