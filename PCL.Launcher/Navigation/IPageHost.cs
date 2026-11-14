using Avalonia.Controls;

namespace PCL.Navigation;

/// <summary>
///     页面宿主：承接某一页面种类的左栏与右栏页面创建、缓存与标题计算。
///     顶级页面提供 <see cref="Tab" />，二级页面为 null；页面实例由宿主缓存以保证往返切换不丢状态。
///     实现类放在各自页面目录中，路由在首次使用时自动发现（无需中央注册表）。
/// </summary>
public interface IPageHost
{
    PageType Page { get; }

    MainTab? Tab { get; }

    double LeftWidth => 210d;

    bool IsSecondary => Tab is null;

    string GetTitle(PageStackData stack);

    Control? GetLeft(PageStackData stack);

    Control GetRight(PageStackData stack);
}