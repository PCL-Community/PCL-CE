namespace PCL.Navigation;

/// <summary>页面位置：顶级页面与可选子页面。用于比较“是否处在同一页面”。</summary>
public readonly record struct PageLocation(PageId Page, SubPageId? SubPage)
{
    public override string ToString()
    {
        return SubPage is { } sub ? $"{Page.Value}/{sub.Value}" : Page.Value;
    }
}

/// <summary>一次页面请求：位置、参数和可选标题覆盖。</summary>
public sealed record PageRoute(
    PageId Page,
    SubPageId? SubPage = null,
    PageParameter? Parameter = null,
    string? TitleOverride = null)
{
    public PageLocation Location => new(Page, SubPage);

    public bool IsSameLocation(PageRoute? other)
    {
        return other is not null && Location == other.Location;
    }

    public override string ToString()
    {
        return Location.ToString();
    }
}