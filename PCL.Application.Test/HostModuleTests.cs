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
            .Build();

        CollectionAssert.Contains(host.ModuleIds.ToArray(), new HostModuleId(SampleHostModule.ModuleId));
        Assert.AreEqual("sample-service", host.Services.GetService(typeof(string)));
        Assert.AreEqual(new ExtensionId("sample.extension"), host.Extensions.Extensions.Single().Id);
        Assert.AreEqual("sample.home", host.Navigation.Pages.Single().Route.Value);
        Assert.IsTrue(host.Commands.TryGetCommand(new CommandId("sample.refresh"), out CommandDescriptor command));
        Assert.AreEqual("刷新", command.Title);
        Assert.AreEqual(new SettingKey("sample.setting"), host.Settings.Settings.Single().Key);
        Assert.AreEqual(new ThemeId("sample.theme"), host.Themes.Themes.Single().Id);
        Assert.AreEqual(new AccountProviderId("sample.account"), host.Accounts.Providers.Single().Id);
        Assert.AreEqual(new DownloadSourceId("sample.download"), host.Downloads.Sources.Single().Id);
        Assert.AreEqual(typeof(SampleLaunchMiddleware), host.Launching.MiddlewareTypes.Single());
    }

    [TestMethod]
    public void AddModule_RejectsDuplicateModuleId()
    {
        PclHostBuilder builder = new();
        builder.AddModule(new SampleHostModule());

        Assert.ThrowsExactly<InvalidOperationException>(() => builder.AddModule(new SampleHostModule()));
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
        Assert.IsTrue(host.Navigation.Pages.Any(static page => page.Route == "sample.static.home"));
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

        ExtensionRegistry extensions = new();
        extensions.AddExtension(new ExtensionDescriptor("sample.extension", "扩展"));

        Assert.ThrowsExactly<InvalidOperationException>(() => extensions.AddExtension(new ExtensionDescriptor("SAMPLE.EXTENSION", "扩展")));

        ThemeRegistry themes = new();
        themes.AddTheme(new ThemeDescriptor
        {
            Id = "sample.theme",
            DisplayName = "主题"
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => themes.AddTheme(new ThemeDescriptor
        {
            Id = "SAMPLE.THEME",
            DisplayName = "主题"
        }));

        AccountProviderRegistry accounts = new();
        accounts.AddProvider(new AccountProviderDescriptor
        {
            Id = "sample.account",
            DisplayName = "账号",
            ProviderType = typeof(SampleAccountProvider)
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => accounts.AddProvider(new AccountProviderDescriptor
        {
            Id = "SAMPLE.ACCOUNT",
            DisplayName = "账号",
            ProviderType = typeof(SampleAccountProvider)
        }));

        DownloadSourceRegistry downloads = new();
        downloads.AddSource(new DownloadSourceDescriptor
        {
            Id = "sample.download",
            DisplayName = "下载源",
            BaseUri = new Uri("https://example.invalid/")
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => downloads.AddSource(new DownloadSourceDescriptor
        {
            Id = "SAMPLE.DOWNLOAD",
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
    public void Launching_RejectsTypesThatAreNotMiddleware()
    {
        LaunchPipelineBuilder builder = new();

        Assert.ThrowsExactly<ArgumentException>(() => builder.Use(typeof(string)));
    }

    private static NavigationPageDescriptor CreatePage(string route) =>
        new()
        {
            Route = route,
            Title = "页面",
            Provider = new DelegatePageProvider(static (_, _) => new ValueTask<object>(new object()))
        };

    private static CommandDescriptor CreateCommand(string id, string title = "命令") =>
        new(id, title, static (_, _) => ValueTask.CompletedTask);

    private sealed class SampleHostModule : IPclHostModule
    {
        public const string ModuleId = "sample.host";

        public HostModuleId Id => new(ModuleId);

        public void Configure(IPclHostBuilder builder)
        {
            builder.Services.AddSingleton("sample-service");
            builder.Extensions.AddExtension(new ExtensionDescriptor("sample.extension", "示例扩展"));
            builder.Navigation.AddPage(CreatePage("sample.home"));
            builder.Commands.AddCommand(CreateCommand("sample.refresh", "刷新"));
            builder.Settings.AddSetting(new SettingDescriptor("sample.setting", "示例设置"));
            builder.Themes.AddTheme(new ThemeDescriptor
            {
                Id = "sample.theme",
                DisplayName = "示例主题"
            });
            builder.Accounts.AddProvider(new AccountProviderDescriptor
            {
                Id = "sample.account",
                DisplayName = "示例账号",
                ProviderType = typeof(SampleAccountProvider)
            });
            builder.Downloads.AddSource(new DownloadSourceDescriptor
            {
                Id = "sample.download",
                DisplayName = "示例下载源",
                BaseUri = new Uri("https://example.invalid/"),
                Kind = DownloadSourceKind.Metadata
            });
            builder.Launching.Use<SampleLaunchMiddleware>();
        }
    }

    private sealed class SampleAccountProvider;

    private sealed class SampleLaunchMiddleware : ILaunchMiddleware
    {
        public ValueTask InvokeAsync(
            LaunchContext context,
            LaunchPipelineNext nextMiddleware,
            CancellationToken cancellationToken) =>
            nextMiddleware(context, cancellationToken);
    }
}
