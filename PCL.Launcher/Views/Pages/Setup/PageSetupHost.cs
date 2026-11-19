using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Navigation;

namespace PCL.Views.Pages.Setup;

public sealed class PageSetupHost : IPageHost
{
    private readonly PageDescriptor _descriptor;
    private readonly PageSetupLeft _left;
    private readonly PageViewFactory _pageViews;
    private readonly Dictionary<SubPageId, Control> _rightPages = new();

    public PageSetupHost(
        PageDescriptor descriptor,
        PageSetupLeft left,
        INavigationService navigation,
        PageViewFactory pageViews)
    {
        _descriptor = descriptor;
        _pageViews = pageViews;
        _left = left;
        _left.PageSelected += sub =>
            navigation.GoTo(new PageLocation(PageIds.Setup, sub));
        _left.AttachPageProvider(GetOrCreatePage);
    }

    public PageId Page => PageIds.Setup;

    public MainTab? Tab => MainTab.Setup;

    public double LeftWidth => _descriptor.LeftWidth;

    public string GetTitle(PageRoute stack)
    {
        var subPage = _descriptor.NormalizeSubPage(stack.SubPage);
        var descriptor = _descriptor.FindSubPage(subPage);
        return descriptor is null ? string.Empty : Lang.Text(descriptor.TitleKey);
    }

    public Control? GetLeft(PageRoute stack)
    {
        var subPage = _descriptor.NormalizeSubPage(stack.SubPage);
        _left.Select(subPage);
        return _left;
    }

    public Control GetRight(PageRoute stack)
    {
        var subPage = _descriptor.NormalizeSubPage(stack.SubPage);
        return GetOrCreatePage(subPage)!;
    }

    private Control? GetOrCreatePage(SubPageId subPage)
    {
        subPage = _descriptor.NormalizeSubPage(subPage);
        if (_rightPages.TryGetValue(subPage, out var cached))
            return cached;

        var descriptor = _descriptor.FindSubPage(subPage);
        if (descriptor is null)
            return null;
        var page = _pageViews.Create(descriptor, new PageRoute(PageIds.Setup, subPage));
        _rightPages[subPage] = page;
        return page;
    }
}