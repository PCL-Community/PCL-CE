// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Accounts;
using PCL.Application.Downloads;
using PCL.Application.Extensions;
using PCL.Application.Hosting;
using PCL.Application.Launching;
using PCL.Application.Settings;
using PCL.UI.Abstractions.Commands;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Pages;
using PCL.UI.Abstractions.Themes;

namespace PCL.Application.Test;

[TestClass]
public sealed class HostModuleTests
{
    [TestMethod]
    public void Build_ReturnsRegistriesPopulatedByHostModule()
    {
        PclHostBuilder builder = new();

        IPclHost host = builder
            .AddModule(new SampleHostModule())
            .AddModule(new HostModuleId("sample.internal"), ConfigureInternalModule)
            .Build();

        CollectionAssert.Contains(host.ModuleIds.ToArray(), new HostModuleId(SampleHostModule.ModuleId));
        Assert.AreEqual("sample-service", host.Services.GetService(typeof(string)));
        Assert.AreEqual(new ExtensionId("sample.extension"), host.Extensions.Extensions.Single().Id);
        Assert.AreEqual("sample.home", host.Navigation.Pages.Single().Route.Value);
        Assert.IsTrue(host.Commands.TryGetCommand(new CommandId("sample.refresh"), out CommandDescriptor command));
        Assert.AreEqual("刷新", command.Title);
        Assert.AreEqual(new SettingKey("sample.setting"), host.Settings.Settings.Single().Key);
        Assert.AreEqual("sample.group", host.SettingsPageGroups.Groups.Single().Id);
        Assert.AreEqual("sample.settings", host.SettingsPages.Pages.Single().Id);
        Assert.AreEqual("sample.group", host.SettingsPages.Pages.Single().GroupId);
        Assert.AreEqual(20, host.SettingsPages.Pages.Single().Order);
        Assert.AreEqual(new ThemeId("sample.theme"), host.Themes.Themes.Single().Id);
        Assert.AreEqual(new AccountProviderId("sample.account"), host.Accounts.Providers.Single().Id);
        Assert.AreEqual(new DownloadSourceId("sample.download"), host.Downloads.Sources.Single().Id);
        Assert.AreEqual(typeof(SampleLaunchMiddleware), host.Launching.MiddlewareTypes.Single());
        Assert.IsInstanceOfType<SampleLaunchMiddleware>(
            host.Launching.Middleware.Single().CreateMiddleware(host.Services));
    }

    [TestMethod]
    public void AddModule_RejectsDuplicateModuleId()
    {
        PclHostBuilder builder = new();
        builder.AddModule(new SampleHostModule());

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.AddModule(new SampleHostModule()));
    }

    [TestMethod]
    public void AddModule_RejectsInvalidOrUnsupportedHostApiRange()
    {
        PclHostBuilder builder = new();

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.AddModule(
            new VersionedHostModule("invalid.range", new HostApiVersion(1, 0), new HostApiVersion(1, 0))));
        Assert.ThrowsExactly<NotSupportedException>(() => builder.AddModule(
            new VersionedHostModule("future.host", new HostApiVersion(1, 0), new HostApiVersion(2, 0))));
        Assert.AreEqual(0, builder.Build().ModuleIds.Count);
    }

    [TestMethod]
    public void AddModule_RegistersStaticModuleWithoutReflection()
    {
        PclHostBuilder builder = new();

        IPclHost host = builder
            .AddModule(
                new HostModuleId("sample.static.host"),
                static hostBuilder => hostBuilder.Navigation.AddPage(CreatePage("sample.static.home")))
            .Build();

        CollectionAssert.Contains(host.ModuleIds.ToArray(), new HostModuleId("sample.static.host"));
        Assert.IsTrue(host.Navigation.Pages.Any(static page => page.Route.Equals("sample.static.home")));
    }

    [TestMethod]
    public void Registries_RejectDuplicateIds()
    {
        NavigationRegistry navigation = new();
        navigation.AddPage(CreatePage("sample.page"));

        Assert.ThrowsExactly<InvalidOperationException>(() => navigation.AddPage(CreatePage("SAMPLE.PAGE")));

        CommandRegistry commands = new();
        commands.AddCommand(CreateCommand("sample.command"));

        Assert.ThrowsExactly<InvalidOperationException>(() => commands.AddCommand(CreateCommand("SAMPLE.COMMAND")));

        SettingsRegistry settings = new();
        settings.AddSetting(new SettingDescriptor("sample.setting", "设置"));

        Assert.ThrowsExactly<InvalidOperationException>(() => settings.AddSetting(new SettingDescriptor("SAMPLE.SETTING", "设置")));

        HostSettingsPageGroupRegistry settingsPageGroups = new();
        settingsPageGroups.AddGroup(new HostSettingsPageGroupDescriptor("sample.group", "示例分组"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            settingsPageGroups.AddGroup(new HostSettingsPageGroupDescriptor("SAMPLE.GROUP", "示例分组")));

        HostSettingsPageRegistry settingsPages = new();
        settingsPages.AddPage(CreateSettingsPage("sample.settings"));
        Assert.ThrowsExactly<InvalidOperationException>(() => settingsPages.AddPage(CreateSettingsPage("SAMPLE.SETTINGS")));

        ExtensionRegistry extensions = new();
        extensions.AddExtension(new ExtensionDescriptor(new ExtensionId("sample.extension"), "扩展"));

        Assert.ThrowsExactly<InvalidOperationException>(() => extensions.AddExtension(new ExtensionDescriptor(new ExtensionId("SAMPLE.EXTENSION"), "扩展")));

        ThemeRegistry themes = new();
        themes.AddTheme(new ThemeDescriptor
        {
            Id = new ThemeId("sample.theme"),
            DisplayName = "主题"
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => themes.AddTheme(new ThemeDescriptor
        {
            Id = new ThemeId("SAMPLE.THEME"),
            DisplayName = "主题"
        }));

        AccountProviderRegistry accounts = new();
        accounts.AddProvider(new AccountProviderDescriptor
        {
            Id = new AccountProviderId("sample.account"),
            DisplayName = "账号",
            ProviderType = typeof(SampleAccountProvider)
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => accounts.AddProvider(new AccountProviderDescriptor
        {
            Id = new AccountProviderId("SAMPLE.ACCOUNT"),
            DisplayName = "账号",
            ProviderType = typeof(SampleAccountProvider)
        }));

        DownloadSourceRegistry downloads = new();
        downloads.AddSource(new DownloadSourceDescriptor
        {
            Id = new DownloadSourceId("sample.download"),
            DisplayName = "下载源",
            BaseUri = new Uri("https://example.invalid/")
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => downloads.AddSource(new DownloadSourceDescriptor
        {
            Id = new DownloadSourceId("SAMPLE.DOWNLOAD"),
            DisplayName = "下载源",
            BaseUri = new Uri("https://example.invalid/")
        }));
    }

    [TestMethod]
    public void Registries_RejectDefaultStrongIds()
    {
        PclHostBuilder hostBuilder = new();
        Assert.ThrowsExactly<ArgumentException>(() => hostBuilder.AddModule(default, static _ => { }));

        CommandRegistry commands = new();
        Assert.ThrowsExactly<ArgumentException>(() => commands.AddCommand(new CommandDescriptor(
            default,
            "命令",
            static (_, _) => ValueTask.CompletedTask)));

        SettingsRegistry settings = new();
        Assert.ThrowsExactly<ArgumentException>(() => settings.AddSetting(new SettingDescriptor(default, "设置")));

        ExtensionRegistry extensions = new();
        Assert.ThrowsExactly<ArgumentException>(() => extensions.AddExtension(new ExtensionDescriptor(default, "扩展")));

        ThemeRegistry themes = new();
        Assert.ThrowsExactly<ArgumentException>(() => themes.AddTheme(new ThemeDescriptor
        {
            Id = default,
            DisplayName = "主题"
        }));

        AccountProviderRegistry accounts = new();
        Assert.ThrowsExactly<ArgumentException>(() => accounts.AddProvider(new AccountProviderDescriptor
        {
            Id = default,
            DisplayName = "账号",
            ProviderType = typeof(SampleAccountProvider)
        }));

        DownloadSourceRegistry downloads = new();
        Assert.ThrowsExactly<ArgumentException>(() => downloads.AddSource(new DownloadSourceDescriptor
        {
            Id = default,
            DisplayName = "下载源",
            BaseUri = new Uri("https://example.invalid/")
        }));
    }

    [TestMethod]
    public void StrongIds_DoNotExposeStringImplicitConversions()
    {
        Type[] idTypes =
        [
            typeof(AccountProviderId),
            typeof(CommandId),
            typeof(DownloadSourceId),
            typeof(ExtensionId),
            typeof(HostModuleId),
            typeof(NavigationRouteId),
            typeof(ThemeId)
        ];

        foreach (Type idType in idTypes)
        {
            bool hasImplicitStringConversion = idType
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Any(method =>
                    method.Name == "op_Implicit" &&
                    method.ReturnType == idType &&
                    method.GetParameters() is [{ ParameterType: var parameterType }] &&
                    parameterType == typeof(string));

            Assert.IsFalse(hasImplicitStringConversion, idType.FullName + " must require explicit ID construction.");
        }
    }

    [TestMethod]
    public void Registries_ExposeStableSnapshotsBetweenMutations()
    {
        CommandRegistry commands = new();
        commands.AddCommand(CreateCommand("sample.command"));
        IReadOnlyList<CommandDescriptor> commandsSnapshot = commands.Commands;

        Assert.AreSame(commandsSnapshot, commands.Commands);
        Assert.IsTrue(commands.TryGetCommand(new CommandId("SAMPLE.COMMAND"), out CommandDescriptor command));
        Assert.AreEqual("sample.command", command.Id.Value);

        commands.AddCommand(CreateCommand("sample.next"));

        Assert.AreNotSame(commandsSnapshot, commands.Commands);
        Assert.IsTrue(commands.RemoveCommand(new CommandId("SAMPLE.NEXT")));
        Assert.IsFalse(commands.TryGetCommand(default, out _));

        ThemeRegistry themes = new();
        themes.AddTheme(new ThemeDescriptor { Id = new ThemeId("sample.z"), DisplayName = "Z", Order = 2 });
        themes.AddTheme(new ThemeDescriptor { Id = new ThemeId("sample.a"), DisplayName = "A", Order = 1 });
        IReadOnlyList<ThemeDescriptor> themesSnapshot = themes.Themes;

        Assert.AreSame(themesSnapshot, themes.Themes);
        CollectionAssert.AreEqual(
            new[] { new ThemeId("sample.a"), new ThemeId("sample.z") },
            themes.Themes.Select(static theme => theme.Id).ToArray());
    }

    [TestMethod]
    public void NavigationRegistry_ReplaceAndRemoveKeepStrongIdIndexInSync()
    {
        NavigationRegistry navigation = new();
        navigation.AddPage(CreatePage("sample.a", order: 2));
        navigation.AddPage(CreatePage("sample.b", order: 1));

        Assert.IsTrue(navigation.ReplacePage(new NavigationRouteId("SAMPLE.A"), CreatePage("sample.c", "替换页面", order: 0)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            navigation.ReplacePage(new NavigationRouteId("sample.c"), CreatePage("sample.b")));

        CollectionAssert.AreEqual(
            new[] { "sample.c", "sample.b" },
            navigation.Pages.Select(static page => page.Route.Value).ToArray());
        Assert.IsTrue(navigation.RemovePage(new NavigationRouteId("SAMPLE.C")));
        Assert.IsFalse(navigation.RemovePage(default));
        CollectionAssert.AreEqual(
            new[] { "sample.b" },
            navigation.Pages.Select(static page => page.Route.Value).ToArray());
    }

    [TestMethod]
    public void Launching_RejectsNullMiddlewareFactories()
    {
        LaunchPipelineBuilder builder = new();

        Assert.ThrowsExactly<ArgumentNullException>(() =>
            builder.Use<SampleLaunchMiddleware>(null!));
    }

    private static NavigationPageDescriptor CreatePage(string route, string title = "页面", int order = 0) =>
        new()
        {
            Route = new NavigationRouteId(route),
            Title = title,
            Order = order,
            Provider = new DelegatePageProvider(static (_, _) => new ValueTask<object>(new object()))
        };

    private static CommandDescriptor CreateCommand(string id, string title = "命令") =>
        new(new CommandId(id), title, static (_, _) => ValueTask.CompletedTask);

    private static HostSettingsPageDescriptor CreateSettingsPage(string id) =>
        new(id, "插件", "lucide/plug", "插件模块", "由 Host Module 注册。", [])
        {
            GroupId = "sample.group",
            Order = 20
        };

    private static void ConfigureInternalModule(PclHostBuilder builder)
    {
        builder.Services.AddSingleton("sample-service");
        builder.Navigation.AddPage(CreatePage("sample.home"));
        builder.Commands.AddCommand(CreateCommand("sample.refresh", "刷新"));
        builder.Settings.AddSetting(new SettingDescriptor("sample.setting", "示例设置"));
        builder.Themes.AddTheme(new ThemeDescriptor
        {
            Id = new ThemeId("sample.theme"),
            DisplayName = "示例主题"
        });
        builder.Accounts.AddProvider(new AccountProviderDescriptor
        {
            Id = new AccountProviderId("sample.account"),
            DisplayName = "示例账号",
            ProviderType = typeof(SampleAccountProvider)
        });
        builder.Downloads.AddSource(new DownloadSourceDescriptor
        {
            Id = new DownloadSourceId("sample.download"),
            DisplayName = "示例下载源",
            BaseUri = new Uri("https://example.invalid/"),
            Kind = DownloadSourceKind.Metadata
        });
        builder.Launching.Use(static _ => new SampleLaunchMiddleware());
    }

    private sealed class SampleHostModule : IPclHostModule
    {
        public const string ModuleId = "sample.host";

        public HostModuleId Id => new(ModuleId);

        public HostApiVersion MinimumHostApiVersion => new(0, 1);

        public HostApiVersion MaximumHostApiVersionExclusive => new(1, 0);

        public void Configure(IPclHostBuilder builder)
        {
            builder.AddExtension(new ExtensionDescriptor(new ExtensionId("sample.extension"), "示例扩展"));
            builder.AddSettingsPageGroup(new HostSettingsPageGroupDescriptor("sample.group", "示例分组"));
            builder.AddSettingsPage(CreateSettingsPage("sample.settings"));
        }
    }

    private sealed class SampleAccountProvider;

    private sealed class VersionedHostModule(
        string id,
        HostApiVersion minimum,
        HostApiVersion maximumExclusive) : IPclHostModule
    {
        public HostModuleId Id { get; } = new(id);

        public HostApiVersion MinimumHostApiVersion { get; } = minimum;

        public HostApiVersion MaximumHostApiVersionExclusive { get; } = maximumExclusive;

        public void Configure(IPclHostBuilder builder) => throw new AssertFailedException(
            "An incompatible HostModule must be rejected before Configure is called.");
    }

    private sealed class SampleLaunchMiddleware : ILaunchMiddleware
    {
        public ValueTask InvokeAsync(
            LaunchContext context,
            LaunchPipelineNext nextMiddleware,
            CancellationToken cancellationToken) =>
            nextMiddleware(context, cancellationToken);
    }
}
