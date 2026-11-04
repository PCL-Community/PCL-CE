using Avalonia.Controls;
using PCL.Navigation;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     启动页宿主：左侧为实例与账号操作区，右侧为主页内容区。
///     两个页面实例均由宿主缓存，主入口往返切换时不重建页面状态。
/// </summary>
public sealed class PageLaunchHost : IPageHost
{
    private PageLaunchLeft? _left;
    private PageLaunchRight? _right;

    public PageType Page => PageType.Launch;

    public MainTab? Tab => MainTab.Launch;

    public double LeftWidth => 300d;

    public string GetTitle(PageStackData stack) => string.Empty;

    public Control? GetLeft(PageStackData stack) => _left ??= new PageLaunchLeft();

    public Control GetRight(PageStackData stack) => _right ??= new PageLaunchRight();
}
