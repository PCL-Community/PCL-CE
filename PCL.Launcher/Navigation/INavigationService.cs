namespace PCL.Navigation;

/// <summary>一次导航定位：顶级页面与子页面种类。</summary>
public readonly record struct RouteKey(PageType Page, PageSubType Sub = PageSubType.Default);

/// <summary>
///     页面/视图模型使用的导航约定。实现由 <see cref="NavigationService" /> 提供，
///     页面不直接依赖 <see cref="PageRouter" /> 的静态入口。
/// </summary>
public interface INavigationService
{
    RouteKey Current { get; }

    event Action<RouteKey>? Navigated;

    void GoTo(RouteKey route, object? parameter = null);

    void GoBack();
}

/// <summary>
///     <see cref="PageRouter" /> 的薄适配层，供页面与 ViewModel 使用。
///     不引入服务容器；页面构造时可直接使用 <see cref="Instance" />。
/// </summary>
public sealed class NavigationService : INavigationService
{
    private NavigationService()
    {
    }

    public static NavigationService Instance { get; } = new();

    public RouteKey Current => new(PageRouter.Current.Page, PageRouter.Current.SubType);

    public event Action<RouteKey>? Navigated;

    public void GoTo(RouteKey route, object? parameter = null)
    {
        PageRouter.Navigate(route.Page, route.Sub, parameter);
    }

    public void GoBack()
    {
        PageRouter.Back();
    }

    internal void RaiseNavigated(RouteKey route)
    {
        Navigated?.Invoke(route);
    }
}