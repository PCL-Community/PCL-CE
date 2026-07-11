// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Application.Hosting;
using PCL.UI.Abstractions.Navigation;

namespace PCL.Desktop.Hosting;

internal static partial class DesktopNavigationRegistry
{
    public const string LaunchRouteValue = "pcl.launch";
    public const string DownloadRouteValue = "pcl.download";
    public const string CommunityRouteValue = "pcl.community";
    public const string SettingsRouteValue = "pcl.settings";

    public static NavigationRouteId LaunchRoute => new(LaunchRouteValue);
    public static NavigationRouteId DownloadRoute => new(DownloadRouteValue);
    public static NavigationRouteId CommunityRoute => new(CommunityRouteValue);
    public static NavigationRouteId SettingsRoute => new(SettingsRouteValue);

    public static partial void RegisterGeneratedHostModules(PclHostBuilder builder);

    [DesktopNavigationPage("pcl.builtin.launch", LaunchRouteValue, "启动", "lucide/play", 0)]
    private static DesktopMainPage CreateLaunchPage(DesktopPageContext context) =>
        context.CreateLaunchPage();

    [DesktopNavigationPage("pcl.builtin.download", DownloadRouteValue, "下载", "lucide/pickaxe", 10)]
    private static DesktopMainPage CreateDownloadPage(DesktopPageContext context) =>
        context.CreateDownloadPage();

    [DesktopNavigationPage("pcl.builtin.community", CommunityRouteValue, "社区", "lucide/download", 20)]
    private static DesktopMainPage CreateCommunityPage(DesktopPageContext context) =>
        context.CreateCommunityPage();

    [DesktopNavigationPage("pcl.builtin.settings", SettingsRouteValue, "设置", "lucide/settings", 40)]
    private static DesktopMainPage CreateSettingsPage(DesktopPageContext context) =>
        context.CreateSettingsPage();
}
