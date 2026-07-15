// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting;
using PCL.Application.Hosting.RuntimeExtensions;
using PCL.Application.Launching;
using PCL.Platform.Paths;
using PCL.Platform.Processes;
using PCL.Platform.Security;
using PCL.UI.Abstractions.Navigation;
using PCL.UI.Abstractions.Pages;

namespace PCL.Desktop.Hosting;

internal static class DesktopHost
{
    private static IPclHost? _current;
    private static IReadOnlyList<IRuntimeExtension> _runtimeExtensions = [];

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
        foreach (IPclHostModule module in EmbeddedRuntimeExtensionLoader.LoadHostModules())
            builder.AddModule(module);
        _current = builder.Build();
        DesktopHostNavigation.Instance.Initialize(_current.Navigation);
        DefaultPlatformPathProvider platformPaths = new();
        RuntimeExtensionHostAccess.Initialize(new RuntimeExtensionHost(
            _current.SettingsPageGroups,
            _current.SettingsPages,
            AvaloniaHostWorkQueue.Instance,
            DesktopHostNotifications.Instance,
            DesktopHostInstanceQuery.Instance,
            DesktopHostUiComposition.Instance,
            DesktopHostDeveloperDiagnostics.Instance,
            DesktopHostNavigation.Instance,
            DesktopHostRawUiAccess.Instance,
            new DesktopHostSecureStorage(new DefaultSecureStorage(platformPaths.ApplicationDataDirectory)),
            DesktopHostUriLauncher.Instance,
            platformPaths.ApplicationDataDirectory,
            platformPaths.CacheDirectory,
            gameSessions: GameSessionRegistry.Shared,
            processes: new DefaultProcessService(),
            clipboard: DesktopHostClipboard.Instance,
            accounts: _current.Accounts,
            downloads: _current.Downloads,
            launching: _current.Launching));
        _runtimeExtensions = EmbeddedRuntimeExtensionLoader.LoadRuntimeExtensions();
        RuntimeExtensionContext extensionContext = new(
            platformPaths.ApplicationDataDirectory,
            platformPaths.CacheDirectory);
        foreach (IRuntimeExtension extension in _runtimeExtensions)
            extension.Initialize(_current, extensionContext);
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
