namespace PCL.Navigation;

/// <summary>
/// 页面/视图模型使用的导航约定。实现由 <see cref="NavigationService" /> 提供， 页面不直接依赖
/// <see cref="PageRouter" /> 的静态入口。
/// </summary>
public interface INavigationService
{
    PageLocation Current { get; }

    event Action<PageLocation>? Navigated;

    void GoTo(PageLocation route, PageParameter? parameter = null);

    void GoBack();
}

/// <summary>
/// <see cref="PageRouter" /> 的薄适配层，供页面与 ViewModel 使用。 不引入服务容器；页面构造时可直接使用
/// <see cref="Instance" />。
/// </summary>
public sealed class NavigationService : INavigationService
{
    private NavigationService()
    {
    }

    public static NavigationService Instance { get; } = new();

    public PageLocation Current => PageRouter.Current.Location;

    public event Action<PageLocation>? Navigated;

    public void GoTo(PageLocation route, PageParameter? parameter = null)
    {
        PageRouter.Navigate(route, parameter);
    }

    public void GoBack()
    {
        PageRouter.Back();
    }

    internal void RaiseNavigated(PageLocation route)
    {
        Navigated?.Invoke(route);
    }
}