using Microsoft.Extensions.DependencyInjection;
using PCL.Views.Pages.Launch;

namespace PCL.Navigation.Catalog;

public sealed class LaunchPageModule : IPageModule
{
    public IReadOnlyList<PageDescriptor> Pages { get; } =
    [
        new(
            PageIds.Launch,
            "Main.Title.Launch",
            "lucide/play",
            MainTab.Launch,
            300d,
            null,
            [],
            [],
            (sp, _) => ActivatorUtilities.CreateInstance<PageLaunchHost>(sp))
    ];

    public IReadOnlyList<MainTabDescriptor> MainTabs { get; } =
    [
        new(
            MainTab.Launch,
            PageIds.Launch,
            "Main.Tab.Launch",
            "lucide/play",
            0,
            0.9d)
    ];
}