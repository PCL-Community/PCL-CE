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

        CollectionAssert.Contains(host.ModuleIds.ToArray(), SampleHostModule.ModuleId);
        Assert.AreEqual("sample-service", host.Services.GetService(typeof(string)));
        Assert.AreEqual("sample.extension", host.Extensions.Extensions.Single().Id);
        Assert.AreEqual("sample.home", host.Navigation.Pages.Single().Route);
        Assert.IsTrue(host.Commands.TryGetCommand("sample.refresh", out CommandDescriptor command));
        Assert.AreEqual("刷新", command.Title);
        Assert.AreEqual("sample.setting", host.Settings.Settings.Single().Key);
        Assert.AreEqual("sample.theme", host.Themes.Themes.Single().Id);
        Assert.AreEqual("sample.account", host.Accounts.Providers.Single().Id);
        Assert.AreEqual("sample.download", host.Downloads.Sources.Single().Id);
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
    }

    [TestMethod]
    public void Launching_RejectsTypesThatAreNotMiddleware()
    {
        LaunchPipelineBuilder builder = new();

        Assert.ThrowsExactly<ArgumentException>(() => builder.Use(typeof(string)));
    }

    [TestMethod]
    public void HostModuleLoader_LoadsPlainHostModuleAssembly()
    {
        PclHostBuilder builder = new();

        HostModuleLoadResult result = HostModuleLoader.LoadFromAssemblyPaths(
            builder,
            [typeof(LoadableHostModule).Assembly.Location]);

        Assert.IsTrue(result.IsSuccessful, string.Join(Environment.NewLine, result.Failures.Select(static failure => failure.Message)));
        CollectionAssert.Contains(result.LoadedModuleIds.ToArray(), LoadableHostModule.ModuleId);

        IPclHost host = builder.Build();
        CollectionAssert.Contains(host.ModuleIds.ToArray(), LoadableHostModule.ModuleId);
        Assert.IsTrue(host.Navigation.Pages.Any(static page => page.Route == "loadable.home"));
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

        public string Id => ModuleId;

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

public sealed class LoadableHostModule : IPclHostModule
{
    public const string ModuleId = "sample.loadable.host";

    public string Id => ModuleId;

    public void Configure(IPclHostBuilder builder)
    {
        builder.Navigation.AddPage(new NavigationPageDescriptor
        {
            Route = "loadable.home",
            Title = "可加载页面",
            Provider = new DelegatePageProvider(static (_, _) => new ValueTask<object>(new object()))
        });
    }
}
