// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting;
using PCL.Application.Hosting.PluginPlatform;
using PCL.Platform.Paths;
using PCL.Platform.Processes;
using PCL.Platform.Security;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Pages;

namespace PCL.Desktop.Hosting;

internal static class DesktopHost
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
        DesktopNavigationRegistry.RegisterGeneratedHostModules(builder);
        foreach (IPclHostModule module in EmbeddedPluginLoader.LoadHostModules())
            builder.AddModule(module);
        _current = builder.Build();
        DesktopPluginHostNavigation.Instance.Initialize(_current.Navigation);
        // Narrow internal bridge for PCL.Plugin (design §3). Not part of public SDK ABI.
        DefaultPlatformPathProvider platformPaths = new();
        PluginPlatformHostAccess.Initialize(new PclPluginPlatformHost(
            _current.SettingsPageGroups,
            _current.SettingsPages,
            AvaloniaPluginHostWorkQueue.Instance,
            DesktopPluginHostNotifications.Instance,
            DesktopPluginHostInstanceQuery.Instance,
            DesktopPluginHostUiComposition.Instance,
            DesktopPluginHostDeveloperDiagnostics.Instance,
            DesktopPluginHostNavigation.Instance,
            DesktopPluginHostRawUiAccess.Instance,
            new DesktopPluginHostSecureStorage(new DefaultSecureStorage(platformPaths.ApplicationDataDirectory)),
            DesktopPluginHostUriLauncher.Instance,
            platformPaths.ApplicationDataDirectory,
            platformPaths.CacheDirectory));
        // Third-party .pnp catalog + load enabled plugins (no-op when plugin DLL is not embedded).
        EmbeddedPluginLoader.InitializeRuntime();
    }
}

internal static class DesktopNavigationModule
{
    public static void AddPage(
        INavigationRegistry navigation,
        NavigationRouteId route,
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
