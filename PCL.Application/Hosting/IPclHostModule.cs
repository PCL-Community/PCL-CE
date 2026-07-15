// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Accounts;
using PCL.Application.Downloads;
using PCL.Application.Extensions;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.UI.Abstractions.Commands;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Themes;

namespace PCL.Application.Hosting;

public readonly record struct HostApiVersion(int Major, int Minor) : IComparable<HostApiVersion>
{
    public int CompareTo(HostApiVersion other)
    {
        int majorComparison = Major.CompareTo(other.Major);
        return majorComparison != 0 ? majorComparison : Minor.CompareTo(other.Minor);
    }

    public override string ToString() => $"{Major}.{Minor}";

    public static bool operator <(HostApiVersion left, HostApiVersion right) => left.CompareTo(right) < 0;

    public static bool operator >(HostApiVersion left, HostApiVersion right) => left.CompareTo(right) > 0;

    public static bool operator <=(HostApiVersion left, HostApiVersion right) => left.CompareTo(right) <= 0;

    public static bool operator >=(HostApiVersion left, HostApiVersion right) => left.CompareTo(right) >= 0;
}

public static class PclHostApi
{
    public static HostApiVersion Current { get; } = new(0, 3);
}

public sealed record RuntimeExtensionContext(
    string ApplicationDataDirectory,
    string CacheDirectory);

public interface IRuntimeExtension
{
    void Initialize(IPclHost host, RuntimeExtensionContext context);

    ValueTask ShutdownAsync(CancellationToken cancellationToken = default);
}

public interface IPclHostBuilder
{
    HostApiVersion ApiVersion { get; }

    void AddExtension(ExtensionDescriptor descriptor);

    void AddSettingsPageGroup(HostSettingsPageGroupDescriptor descriptor);

    void AddSettingsPage(HostSettingsPageDescriptor descriptor);
}

public interface IPclHostModule
{
    HostModuleId Id { get; }

    HostApiVersion MinimumHostApiVersion { get; }

    HostApiVersion MaximumHostApiVersionExclusive { get; }

    void Configure(IPclHostBuilder builder);
}

public interface IPclHost
{
    IServiceProvider Services { get; }

    IExtensionRegistry Extensions { get; }

    INavigationRegistry Navigation { get; }

    ICommandRegistry Commands { get; }

    ISettingsRegistry Settings { get; }

    IHostSettingsPageGroupRegistry SettingsPageGroups { get; }

    IHostSettingsPageRegistry SettingsPages { get; }

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

    public HostApiVersion ApiVersion => PclHostApi.Current;

    public IServiceRegistry Services { get; } = new ServiceRegistry();

    public IExtensionRegistry Extensions { get; } = new ExtensionRegistry();

    public INavigationRegistry Navigation { get; } = new NavigationRegistry();

    public ICommandRegistry Commands { get; } = new CommandRegistry();

    public ISettingsRegistry Settings { get; } = new SettingsRegistry();

    public IHostSettingsPageGroupRegistry SettingsPageGroups { get; } = new HostSettingsPageGroupRegistry();

    public IHostSettingsPageRegistry SettingsPages { get; } = new HostSettingsPageRegistry();

    public IThemeRegistry Themes { get; } = new ThemeRegistry();

    public IAccountProviderRegistry Accounts { get; } = new AccountProviderRegistry();

    public IDownloadSourceRegistry Downloads { get; } = new DownloadSourceRegistry();

    public ILaunchPipelineBuilder Launching { get; } = new LaunchPipelineBuilder();

    public PclHostBuilder AddModule(HostModuleId id, Action<PclHostBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (string.IsNullOrWhiteSpace(id.Value))
            throw new ArgumentException("Host Module ID 不能为空。", nameof(id));
        if (_moduleIdSet.Contains(id.Value))
            throw new InvalidOperationException($"Host Module 已注册：{id.Value}");

        configure(this);
        RegisterModuleId(id);
        return this;
    }

    public PclHostBuilder AddModule(IPclHostModule hostModule)
    {
        ArgumentNullException.ThrowIfNull(hostModule);
        ValidateHostApiRange(hostModule);
        if (_moduleIdSet.Contains(hostModule.Id.Value))
            throw new InvalidOperationException($"Host Module 已注册：{hostModule.Id.Value}");

        hostModule.Configure(this);
        RegisterModuleId(hostModule.Id);
        return this;
    }

    public void AddExtension(ExtensionDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Extensions.AddExtension(descriptor);
    }

    public void AddSettingsPageGroup(HostSettingsPageGroupDescriptor descriptor) => SettingsPageGroups.AddGroup(descriptor);

    public void AddSettingsPage(HostSettingsPageDescriptor descriptor) => SettingsPages.AddPage(descriptor);

    public IPclHost Build() =>
        new PclHost(
            Services,
            Extensions,
            Navigation,
            Commands,
            Settings,
            SettingsPageGroups,
            SettingsPages,
            Themes,
            Accounts,
            Downloads,
            Launching,
            _moduleIds.ToArray());

    private void RegisterModuleId(HostModuleId id)
    {
        _moduleIdSet.Add(id.Value);
        _moduleIds.Add(id);
    }

    private void ValidateHostApiRange(IPclHostModule hostModule)
    {
        if (hostModule.MinimumHostApiVersion >= hostModule.MaximumHostApiVersionExclusive)
        {
            throw new InvalidOperationException(
                $"Host Module '{hostModule.Id}' 声明了无效的 Host API 范围：" +
                $"[{hostModule.MinimumHostApiVersion}, {hostModule.MaximumHostApiVersionExclusive})。");
        }

        if (ApiVersion < hostModule.MinimumHostApiVersion || ApiVersion >= hostModule.MaximumHostApiVersionExclusive)
        {
            throw new NotSupportedException(
                $"Host Module '{hostModule.Id}' 不支持 PCL N Host API {ApiVersion}；" +
                $"需要 [{hostModule.MinimumHostApiVersion}, {hostModule.MaximumHostApiVersionExclusive})。");
        }
    }
}

internal sealed class PclHost(
    IServiceProvider services,
    IExtensionRegistry extensions,
    INavigationRegistry navigation,
    ICommandRegistry commands,
    ISettingsRegistry settings,
    IHostSettingsPageGroupRegistry settingsPageGroups,
    IHostSettingsPageRegistry settingsPages,
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

    public IHostSettingsPageGroupRegistry SettingsPageGroups { get; } = settingsPageGroups;

    public IHostSettingsPageRegistry SettingsPages { get; } = settingsPages;

    public IThemeRegistry Themes { get; } = themes;

    public IAccountProviderRegistry Accounts { get; } = accounts;

    public IDownloadSourceRegistry Downloads { get; } = downloads;

    public ILaunchPipelineBuilder Launching { get; } = launching;

    public IReadOnlyList<HostModuleId> ModuleIds { get; } = moduleIds;
}
