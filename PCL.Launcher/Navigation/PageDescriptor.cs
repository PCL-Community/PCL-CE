using Avalonia.Controls;

namespace PCL.Navigation;

/// <summary>顶级页面中的分组标题。</summary>
public sealed record PageGroupDescriptor(
    string Key,
    string TitleKey,
    int Order);

/// <summary>二级页面描述。标题、图标、分组、排序、可见性和创建方式集中在这里。</summary>
public sealed record SubPageDescriptor(
    SubPageId Id,
    string TitleKey,
    string? IconKey,
    string? GroupKey,
    int Order,
    Func<IServiceProvider, PageRoute, Control> CreateRight,
    Func<bool>? IsVisible = null,
    bool CanRefresh = false,
    string? ResetMessageKey = null,
    double LogoScale = 1d);

/// <summary>顶级页面描述。Host 由描述表创建，标题栏和左栏通过它获取元数据。</summary>
public sealed record PageDescriptor(
    PageId Id,
    string TitleKey,
    string? IconKey,
    MainTab? Tab,
    double LeftWidth,
    SubPageId? DefaultSubPage,
    IReadOnlyList<PageGroupDescriptor> Groups,
    IReadOnlyList<SubPageDescriptor> SubPages,
    Func<IServiceProvider, PageDescriptor, IPageHost>? CreateHost = null)
{
    public SubPageDescriptor? FindSubPage(SubPageId id)
    {
        return id.Parent == Id
            ? SubPages.FirstOrDefault(page => page.Id == id)
            : null;
    }

    /// <summary>将空子页、未知子页或隐藏子页归一化到当前页面的第一个可见子页。</summary>
    public SubPageId NormalizeSubPage(SubPageId? sub)
    {
        return sub switch
        {
            { } requested
                when FindSubPage(requested) is { } descriptor && IsVisible(descriptor) =>
                requested,
            null when DefaultSubPage is { } defaultSub
                && FindSubPage(defaultSub) is { } defaultDescriptor
                && IsVisible(defaultDescriptor) =>
                defaultSub,
            _ => FirstVisibleSubPage()
        };
    }

    public SubPageId FirstVisibleSubPage()
    {
        var visible = SubPages
            .Where(IsVisible)
            .OrderBy(page => page.Order)
            .Select(page => (SubPageId?)page.Id)
            .FirstOrDefault();
        return visible
            ?? SubPages
                .OrderBy(page => page.Order)
                .Select(page => page.Id)
                .FirstOrDefault();
    }

    public IReadOnlyList<SubPageDescriptor> VisibleSubPages(bool forceShow = false)
    {
        return
        [
            .. SubPages
                .Where(page => forceShow || IsVisible(page))
                .OrderBy(page => page.Order)
        ];
    }

    public int VisibleSubPageCount(bool forceShow = false)
    {
        return VisibleSubPages(forceShow).Count;
    }

    public static bool IsVisible(SubPageDescriptor page)
    {
        return page.IsVisible?.Invoke() ?? true;
    }
}

/// <summary>标题栏主入口描述。</summary>
public sealed record MainTabDescriptor(
    MainTab Id,
    PageId DefaultPage,
    string TitleKey,
    string IconKey,
    int Order,
    double LogoScale = 1d);