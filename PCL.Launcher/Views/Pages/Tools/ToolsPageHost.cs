using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Tools;

/// <summary>联机与工具页宿主：左栏常驻一份，右栏按子页描述缓存。</summary>
public sealed class ToolsPageHost : IPageHost
{
    private readonly PageDescriptor _descriptor = PageCatalog.RequirePage(PageIds.Tools);
    private readonly PageToolsLeft _left;
    private readonly Dictionary<SubPageId, Control> _rightPages = new();

    public ToolsPageHost()
    {
        _left = new PageToolsLeft();
        _left.PageSelected += sub => PageRouter.Navigate(PageIds.Tools, sub);
    }

    public PageId Page => PageIds.Tools;

    public MainTab? Tab => MainTab.Tools;

    public double LeftWidth => _descriptor.LeftWidth;

    public string GetTitle(PageRoute stack)
    {
        var sub = _descriptor.NormalizeSubPage(stack.SubPage);
        var descriptor = _descriptor.FindSubPage(sub);
        return descriptor is null ? string.Empty : Lang.Text(descriptor.TitleKey);
    }

    public Control? GetLeft(PageRoute stack)
    {
        var sub = _descriptor.NormalizeSubPage(stack.SubPage);
        _left.Select(sub);
        return _left;
    }

    public Control GetRight(PageRoute stack)
    {
        var sub = _descriptor.NormalizeSubPage(stack.SubPage);
        if (_rightPages.TryGetValue(sub, out var cached))
            return cached;

        var descriptor = _descriptor.FindSubPage(sub);
        if (descriptor is null)
            throw new InvalidOperationException($"未找到工具子页：{sub}");
        var page = descriptor.CreateRight(stack);
        _rightPages[sub] = page;
        return page;
    }
}