namespace PCL;

public readonly record struct RouteKey(FormMain.PageType Page, FormMain.PageSubType Sub = FormMain.PageSubType.Default);

public interface INavigationService
{
    RouteKey Current { get; }
    event Action<RouteKey>? Navigated;
    void GoTo(RouteKey route, object? parameter = null);
    void GoBack();
}

public sealed class NavigationService : INavigationService
{
    private readonly FormMain _host;

    public NavigationService(FormMain host)
    {
        _host = host;
    }

    public RouteKey Current => new(_host.pageCurrent.page, _host.PageCurrentSub);

    public event Action<RouteKey>? Navigated;

    public void GoTo(RouteKey route, object? parameter = null)
    {
        _host.PageChange(new FormMain.PageStackData { page = route.Page }, route.Sub, parameter);
    }

    public void GoBack()
    {
        _host.PageBack();
    }

    internal void RaiseNavigated(RouteKey route) => Navigated?.Invoke(route);
}
