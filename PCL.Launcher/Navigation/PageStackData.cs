namespace PCL.Navigation;

/// <summary>
///     一次页面定位：顶级页面种类、二级页面种类与可选参数。
///     相等性只比较页面种类与参数（子页面实例），二级页面种类由 <see cref="IsSameLocation" /> 单独比较，
///     这样返回栈可以用“同一页面 + 同一参数”去重。
/// </summary>
public sealed class PageStackData : IEquatable<PageStackData>
{
    public PageStackData()
    {
    }

    public PageStackData(
        PageType page,
        PageSubType subType = PageSubType.Default,
        object? parameter = null,
        string? titleOverride = null)
    {
        Page = page;
        SubType = subType;
        Parameter = parameter;
        TitleOverride = titleOverride;
    }

    public PageType Page { get; init; } = PageType.Launch;

    public PageSubType SubType { get; init; }

    public object? Parameter { get; init; }

    public string? TitleOverride { get; init; }

    public bool Equals(PageStackData? other)
    {
        return other is not null
            && Page == other.Page
            && string.Equals(TitleOverride, other.TitleOverride, StringComparison.Ordinal)
            && Equals(Parameter, other.Parameter);
    }

    public bool IsSameLocation(PageStackData? other)
    {
        return other is not null && Equals(other) && SubType == other.SubType;
    }

    public override bool Equals(object? obj)
    {
        return obj is PageStackData other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Page, Parameter, TitleOverride);
    }

    public override string ToString()
    {
        return $"{Page}/{SubType}";
    }

    public static implicit operator PageStackData(PageType page)
    {
        return new PageStackData(page);
    }

    public static implicit operator PageType(PageStackData stack)
    {
        return stack.Page;
    }
}