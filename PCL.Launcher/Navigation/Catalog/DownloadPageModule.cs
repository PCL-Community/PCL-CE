using PCL.Views.Pages.Download;
using static PCL.Navigation.PageDescriptorFactory;

namespace PCL.Navigation.Catalog;

public sealed class DownloadPageModule : IPageModule
{
    private static SubPageDescriptor[] DownloadSubPages { get; } =
    [
        Sub(
            PageIds.Download,
            SubPageIds.DownloadInstall,
            "Download.Left.Minecraft",
            "lucide/boxes",
            "minecraft",
            0,
            _ => new PageDownloadInstall()),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadMod,
            "Download.Left.Mod",
            "lucide/puzzle",
            "community",
            1,
            _ => new PageDownloadCompPage(Comp.CompType.Mod),
            refresh: true),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadPack,
            "Download.Left.Modpack",
            "lucide/package",
            "community",
            2,
            _ => new PageDownloadCompPage(Comp.CompType.ModPack),
            refresh: true),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadDataPack,
            "Download.Left.DataPack",
            "lucide/file-archive",
            "community",
            3,
            _ => new PageDownloadCompPage(Comp.CompType.DataPack),
            refresh: true),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadResourcePack,
            "Download.Left.ResourcePack",
            "lucide/layers",
            "community",
            4,
            _ => new PageDownloadCompPage(Comp.CompType.ResourcePack),
            refresh: true),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadShader,
            "Download.Left.Shader",
            "lucide/sparkles",
            "community",
            5,
            _ => new PageDownloadCompPage(Comp.CompType.Shader),
            refresh: true),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadWorld,
            "Download.Left.World",
            "lucide/globe",
            "community",
            6,
            _ => new PageDownloadCompPage(Comp.CompType.World),
            refresh: true),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadCompFavorites,
            "Download.Left.Favorites",
            "lucide/heart",
            "community",
            7,
            _ => new PageDownloadCompFavorites()),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadClient,
            "Download.Left.Minecraft",
            "lucide/package",
            "install",
            8,
            _ => new PageDownloadClient()),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadOptiFine,
            "Download.Left.Optifine",
            "lucide/gauge",
            "install",
            9,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.OptiFine)),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadForge,
            "Download.Left.Forge",
            "lucide/anvil",
            "install",
            10,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.Forge)),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadNeoForge,
            "Download.Left.NeoForge",
            "lucide/cat",
            "install",
            11,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.NeoForge)),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadCleanroom,
            "Download.Left.Cleanroom",
            "lucide/flask-conical",
            "install",
            12,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.Cleanroom)),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadFabric,
            "Download.Left.Fabric",
            "lucide/scroll",
            "install",
            13,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.Fabric)),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadLegacyFabric,
            "Download.Left.LegacyFabric",
            "lucide/scroll",
            "install",
            14,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.LegacyFabric)),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadLabyMod,
            "Download.Left.LabyMod",
            "lucide/box",
            "install",
            15,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.LabyMod)),
        Sub(
            PageIds.Download,
            SubPageIds.DownloadLiteLoader,
            "Download.Left.LiteLoader",
            "lucide/egg",
            "install",
            16,
            _ => new PageDownloadLoaderPage(DownloadLoaderKind.LiteLoader))
    ];

    public IReadOnlyList<PageDescriptor> Pages { get; } =
    [
        new(
            PageIds.Download,
            "Download.Left.CommunityResources",
            "lucide/download",
            MainTab.Download,
            210d,
            SubPageIds.DownloadInstall,
            [
                new PageGroupDescriptor("minecraft", "", 0),
                new PageGroupDescriptor("community", "Download.Left.CommunityResources", 1),
                new PageGroupDescriptor("install", "Download.Left.Installation", 2)
            ],
            DownloadSubPages,
            _ => new PageDownloadHost())
    ];

    public IReadOnlyList<MainTabDescriptor> MainTabs { get; } =
    [
        new(
            MainTab.Download,
            PageIds.Download,
            "Main.Tab.Download",
            "lucide/download",
            1,
            0.9d)
    ];
}