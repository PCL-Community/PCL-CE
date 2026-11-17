namespace PCL.Navigation;

/// <summary>将功能模块聚合为只读页面目录，并在这里集中校验页面描述。</summary>
public static class PageCatalogBuilder
{
    public static PageCatalogSnapshot Build(IEnumerable<IPageModule> modules)
    {
        var pages = modules.SelectMany(module => module.Pages).ToArray();
        var tabs = modules.SelectMany(module => module.MainTabs).ToArray();

        ValidatePages(pages);
        ValidateTabs(pages, tabs);

        return new PageCatalogSnapshot(
            pages,
            [.. tabs.OrderBy(tab => tab.Order)]);
    }

    private static void ValidatePages(IReadOnlyList<PageDescriptor> pages)
    {
        var pageIds = new HashSet<PageId>();
        foreach (var page in pages)
        {
            if (page.Id.IsEmpty)
                throw new InvalidOperationException("页面 ID 不能为空");
            if (!pageIds.Add(page.Id))
                throw new InvalidOperationException($"页面 ID 重复：{page.Id}");
            if (page.CreateHost is null)
                throw new InvalidOperationException($"页面没有 Host 工厂：{page.Id}");

            var subIds = new HashSet<SubPageId>();
            var groupKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in page.Groups)
                if (!groupKeys.Add(group.Key))
                    throw new InvalidOperationException(
                        $"页面 {page.Id} 的分组 key 重复：{group.Key}");

            foreach (var sub in page.SubPages)
            {
                if (sub.Id.Parent != page.Id)
                    throw new InvalidOperationException(
                        $"子页面 {sub.Id} 不属于页面 {page.Id}");
                if (!subIds.Add(sub.Id))
                    throw new InvalidOperationException(
                        $"页面 {page.Id} 的子页面 ID 重复：{sub.Id}");
                if (!string.IsNullOrEmpty(sub.GroupKey) && !groupKeys.Contains(sub.GroupKey))
                    throw new InvalidOperationException(
                        $"子页面 {sub.Id} 引用了不存在的分组：{sub.GroupKey}");
            }

            if (page.DefaultSubPage is { } defaultSubPage)
            {
                if (defaultSubPage.Parent != page.Id || !subIds.Contains(defaultSubPage))
                    throw new InvalidOperationException(
                        $"页面 {page.Id} 的默认子页不存在：{defaultSubPage}");
            }
            else if (page.SubPages.Count > 0)
            {
                throw new InvalidOperationException($"页面 {page.Id} 没有默认子页");
            }

            if (page.SubPages.Count == 0 && page.DefaultSubPage is not null)
                throw new InvalidOperationException($"页面 {page.Id} 没有子页但声明了默认子页");
        }
    }

    private static void ValidateTabs(
        IReadOnlyList<PageDescriptor> pages,
        IReadOnlyList<MainTabDescriptor> tabs)
    {
        var pageIds = pages.Select(page => page.Id).ToHashSet();
        var tabIds = new HashSet<MainTab>();
        var defaultPages = new HashSet<PageId>();
        foreach (var tab in tabs)
        {
            if (!tabIds.Add(tab.Id))
                throw new InvalidOperationException($"主入口重复：{tab.Id}");
            if (!pageIds.Contains(tab.DefaultPage))
                throw new InvalidOperationException(
                    $"主入口 {tab.Id} 指向不存在的页面：{tab.DefaultPage}");
            if (!defaultPages.Add(tab.DefaultPage))
                throw new InvalidOperationException(
                    $"多个主入口指向同一页面：{tab.DefaultPage}");
        }
    }
}