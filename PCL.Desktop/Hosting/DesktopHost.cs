// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Pages;

namespace PCL.Desktop.Hosting;

internal static partial class DesktopHost
{
    private static IPclHost? _current;

    public static IPclHost Current
    {
        get
        {
            Initialize();
            return _current ?? throw new InvalidOperationException("Desktop Host 尚未初始化。");
        }
    }

    public static void Initialize()
    {
        if (_current is not null)
            return;

        PclHostBuilder builder = new();
        builder.AddModule(new BuiltInLaunchModule());
        builder.AddModule(new BuiltInDownloadModule());
        builder.AddModule(new BuiltInCommunityModule());
        builder.AddModule(new BuiltInSettingsModule());
        RegisterInjectedHostModules(builder);
        _current = builder.Build();
    }

    static partial void RegisterInjectedHostModules(PclHostBuilder builder);
}

internal sealed class BuiltInLaunchModule : IPclHostModule
{
    public string Id => "pcl.builtin.launch";

    public void Configure(IPclHostBuilder builder) =>
        DesktopNavigationModule.AddPage(builder.Navigation, "pcl.launch", "启动", "lucide/play", 0,
            static context => context.CreateLaunchPage());
}

internal sealed class BuiltInDownloadModule : IPclHostModule
{
    public string Id => "pcl.builtin.download";

    public void Configure(IPclHostBuilder builder) =>
        DesktopNavigationModule.AddPage(builder.Navigation, "pcl.download", "下载", "lucide/pickaxe", 10,
            static context => context.CreateDownloadPage());
}

internal sealed class BuiltInCommunityModule : IPclHostModule
{
    public string Id => "pcl.builtin.community";

    public void Configure(IPclHostBuilder builder) =>
        DesktopNavigationModule.AddPage(builder.Navigation, "pcl.community", "社区", "lucide/download", 20,
            static context => context.CreatePlaceholderPage("社区"));
}

internal sealed class BuiltInSettingsModule : IPclHostModule
{
    public string Id => "pcl.builtin.settings";

    public void Configure(IPclHostBuilder builder) =>
        DesktopNavigationModule.AddPage(builder.Navigation, "pcl.settings", "设置", "lucide/settings", 40,
            static context => context.CreateSettingsPage());
}

internal static class DesktopNavigationModule
{
    public static void AddPage(
        INavigationRegistry navigation,
        string route,
        string title,
        string icon,
        int order,
        Func<DesktopPageContext, DesktopMainPage> pageFactory)
    {
        ArgumentNullException.ThrowIfNull(pageFactory);
        navigation.AddPage(new NavigationPageDescriptor
        {
            Route = route,
            Title = title,
            Icon = icon,
            Order = order,
            Provider = new DelegatePageProvider((context, _) =>
            {
                if (context.Parameter is not DesktopPageContext desktopContext)
                {
                    throw new InvalidOperationException(
                        $"Desktop 页面 '{context.Route}' 需要 {nameof(DesktopPageContext)} 运行时上下文。");
                }

                return new ValueTask<object>(pageFactory(desktopContext));
            })
        });
    }
}
