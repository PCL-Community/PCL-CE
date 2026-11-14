using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

public sealed class PageDownloadHost : IPageHost
{
    private readonly PageDownloadLeft _left;
    private readonly Dictionary<PageSubType, Control> _rightPages = new();

    public PageDownloadHost()
    {
        _left = new PageDownloadLeft();
        _left.PageSelected += sub => PageRouter.Navigate(PageType.Download, sub);
        _left.RefreshRequested += RefreshPage;
        _left.InstallItemReselected += () =>
        {
            if (_rightPages.TryGetValue(PageSubType.DownloadInstall, out var page)
                && page is PageDownloadInstall install)
                install.ExitSelectPage();
        };
    }

    public PageType Page => PageType.Download;

    public MainTab? Tab => MainTab.Download;

    public double LeftWidth => 210d;

    public string GetTitle(PageStackData stack)
    {
        return string.Empty;
    }

    public Control? GetLeft(PageStackData stack)
    {
        var sub = NormalizeSubPage(stack.SubType);
        _left.Select(sub);
        return _left;
    }

    public Control GetRight(PageStackData stack)
    {
        var sub = NormalizeSubPage(stack.SubType);
        if (_rightPages.TryGetValue(sub, out var cached))
            return cached;

        var page = CreatePage(sub);
        _rightPages[sub] = page;
        return page;
    }

    private void RefreshPage(PageSubType sub)
    {
        try
        {
            var page = GetRight(new PageStackData(PageType.Download, sub));
            if (page is IRefreshable refreshable)
            {
                refreshable.Refresh();
                return;
            }

            LauncherCommon.Log($"下载子页 {sub} 不支持刷新");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新下载子页失败");
        }
    }

    private static PageSubType NormalizeSubPage(PageSubType sub)
    {
        return sub == PageSubType.Default ? PageSubType.DownloadInstall : sub;
    }

    private static Control CreatePage(PageSubType sub)
    {
        return sub switch
        {
            PageSubType.DownloadInstall => new PageDownloadInstall(),
            PageSubType.DownloadClient => new PageDownloadClient(),
            PageSubType.DownloadMod => new PageDownloadCompPage(Comp.CompType.Mod),
            PageSubType.DownloadPack => new PageDownloadCompPage(Comp.CompType.ModPack),
            PageSubType.DownloadDataPack => new PageDownloadCompPage(Comp.CompType.DataPack),
            PageSubType.DownloadResourcePack =>
                new PageDownloadCompPage(Comp.CompType.ResourcePack),
            PageSubType.DownloadShader => new PageDownloadCompPage(Comp.CompType.Shader),
            PageSubType.DownloadWorld => new PageDownloadCompPage(Comp.CompType.World),
            PageSubType.DownloadCompFavorites => new PageDownloadCompFavorites(),
            PageSubType.DownloadOptiFine => new PageDownloadLoaderPage(DownloadLoaderKind.OptiFine),
            PageSubType.DownloadForge => new PageDownloadLoaderPage(DownloadLoaderKind.Forge),
            PageSubType.DownloadNeoForge => new PageDownloadLoaderPage(DownloadLoaderKind.NeoForge),
            PageSubType.DownloadCleanroom => new PageDownloadLoaderPage(
                DownloadLoaderKind.Cleanroom),
            PageSubType.DownloadFabric => new PageDownloadLoaderPage(DownloadLoaderKind.Fabric),
            PageSubType.DownloadLegacyFabric => new PageDownloadLoaderPage(
                DownloadLoaderKind.LegacyFabric),
            PageSubType.DownloadLabyMod => new PageDownloadLoaderPage(DownloadLoaderKind.LabyMod),
            PageSubType.DownloadLiteLoader => new PageDownloadLoaderPage(
                DownloadLoaderKind.LiteLoader),
            _ => throw new Exception(
                Lang.Text("Download.Left.Error.UnknownSubPageType", (int)sub))
        };
    }
}