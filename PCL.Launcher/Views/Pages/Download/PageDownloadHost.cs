using Avalonia.Controls;
using PCL.Navigation;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Download;

public sealed class PageDownloadHost : IPageHost
{
    private readonly PageDescriptor _descriptor;
    private readonly PageDownloadLeft _left;
    private readonly PageViewFactory _pageViews;
    private readonly Dictionary<SubPageId, Control> _rightPages = new();

    public PageDownloadHost(
        PageDescriptor descriptor,
        PageDownloadLeft left,
        INavigationService navigation,
        PageViewFactory pageViews)
    {
        _descriptor = descriptor;
        _pageViews = pageViews;
        _left = left;
        _left.PageSelected += sub =>
            navigation.GoTo(new PageLocation(PageIds.Download, sub));
        _left.RefreshRequested += RefreshPage;
        _left.InstallItemReselected += () =>
        {
            if (_rightPages.TryGetValue(SubPageIds.DownloadInstall, out var page)
                && page is PageDownloadInstall install)
                install.ExitSelectPage();
        };
    }

    public PageId Page => PageIds.Download;

    public MainTab? Tab => MainTab.Download;

    public double LeftWidth => _descriptor.LeftWidth;

    public string GetTitle(PageRoute stack)
    {
        return string.Empty;
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
        {
            PrepareNavigation(cached, stack.Parameter);
            return cached;
        }

        var subDescriptor = _descriptor.FindSubPage(sub);
        if (subDescriptor is null)
            throw new InvalidOperationException($"未找到下载子页：{sub}");
        var page = _pageViews.Create(subDescriptor, stack);
        PrepareNavigation(page, stack.Parameter);
        _rightPages[sub] = page;
        return page;
    }

    private static void PrepareNavigation(Control page, PageParameter? parameter)
    {
        if (page is PageDownloadCompPage compPage)
            compPage.PrepareNavigation(parameter);
    }

    private void RefreshPage(SubPageId subPage)
    {
        try
        {
            var page = GetRight(new PageRoute(PageIds.Download, subPage));
            if (page is IRefreshable refreshable)
            {
                refreshable.Refresh();
                return;
            }

            LauncherCommon.Log($"下载子页 {subPage} 不支持刷新");
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "刷新下载子页失败");
        }
    }
}