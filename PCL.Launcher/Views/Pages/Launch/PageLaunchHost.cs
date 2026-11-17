using Avalonia.Controls;
using PCL.Navigation;

namespace PCL.Views.Pages.Launch;

/// <summary>启动页宿主：左侧为实例与账号操作区，右侧为主页内容区。 两个页面实例均由宿主缓存，主入口往返切换时不重建页面状态。</summary>
public sealed class PageLaunchHost(
    PageLaunchLeft left,
    PageLaunchRight right) : IPageHost
{
    public PageId Page => PageIds.Launch;

    public MainTab? Tab => MainTab.Launch;

    public double LeftWidth => 300d;

    public string GetTitle(PageRoute stack)
    {
        return string.Empty;
    }

    public Control? GetLeft(PageRoute stack)
    {
        return left;
    }

    public Control GetRight(PageRoute stack)
    {
        return right;
    }
}