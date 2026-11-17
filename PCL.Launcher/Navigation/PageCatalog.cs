using PCL.Navigation.Catalog;

namespace PCL.Navigation;

/// <summary>页面目录：聚合功能模块的页面描述，不包含具体页面列表或页面创建 switch。</summary>
public static class PageCatalog
{
    private static readonly Lazy<PageCatalogSnapshot> _snapshot = new(() =>
        PageCatalogBuilder.Build(
        [
            new LaunchPageModule(),
            new DownloadPageModule(),
            new SetupPageModule(),
            new ToolsPageModule(),
            new InstancePageModule(),
            new ShellPageModule()
        ]));

    public static PageCatalogSnapshot Snapshot => _snapshot.Value;

    public static IReadOnlyList<PageDescriptor> Pages => Snapshot.Pages;

    public static IReadOnlyList<MainTabDescriptor> MainTabs => Snapshot.MainTabs;

    public static PageDescriptor? GetPage(PageId page)
    {
        return Pages.FirstOrDefault(descriptor => descriptor.Id == page);
    }

    public static PageDescriptor RequirePage(PageId page)
    {
        return GetPage(page)
            ?? throw new ArgumentOutOfRangeException(nameof(page), page, null);
    }

    public static PageId PageOfTab(MainTab tab)
    {
        return MainTabs.FirstOrDefault(descriptor => descriptor.Id == tab)?.DefaultPage
            ?? throw new ArgumentOutOfRangeException(nameof(tab), tab, null);
    }

    public static MainTab? TabOfPage(PageId page)
    {
        return MainTabs.FirstOrDefault(descriptor => descriptor.DefaultPage == page)?.Id;
    }

    public static SubPageDescriptor? FindSubPage(PageId page, SubPageId sub)
    {
        return GetPage(page)?.FindSubPage(sub);
    }

    public static bool TryParsePage(string? value, out PageId page)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            page = default;
            return false;
        }

        var match = Pages.FirstOrDefault(descriptor => string.Equals(
            descriptor.Id.Value,
            value,
            StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            page = default;
            return false;
        }

        page = match.Id;
        return true;
    }

    public static bool TryParseSubPage(PageId parent, string? value, out SubPageId? subPage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            subPage = null;
            return true;
        }

        var page = GetPage(parent);
        if (page is null)
        {
            subPage = null;
            return false;
        }

        var match = page.SubPages.FirstOrDefault(descriptor => string.Equals(
            descriptor.Id.Value,
            value,
            StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            subPage = null;
            return false;
        }

        subPage = match.Id;
        return true;
    }
}