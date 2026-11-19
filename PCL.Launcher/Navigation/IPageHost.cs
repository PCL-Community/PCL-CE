using Avalonia.Controls;

namespace PCL.Navigation;

/// <summary>
/// 页面宿主：承接某一页面种类的左栏与右栏页面创建、缓存与标题计算。 顶级页面提供 <see cref="Tab" />，二级页面为
/// null；页面实例由宿主缓存以保证往返切换不丢状态。 实现类由 <see cref="PageCatalog" /> 的描述创建，不再依赖程序集反射。
/// </summary>
public interface IPageHost
{
    PageId Page { get; }

    MainTab? Tab { get; }

    double LeftWidth => 210d;

    bool IsSecondary => Tab is null;

    string GetTitle(PageRoute route);

    Control? GetLeft(PageRoute route);

    Control GetRight(PageRoute route);
}