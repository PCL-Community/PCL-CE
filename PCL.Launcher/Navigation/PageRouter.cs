using Avalonia.Controls;
using Avalonia.Threading;
using PCL.Core.Logging;
using PCL.UI.Animations;
using PCL.Views.Pages;

namespace PCL.Navigation;

/// <summary>
///     页面路由：把页面内容写入主窗口的页面宿主控件。
///     页面必须在 UI 线程创建，非 UI 线程调用时会整体调度到 UI 线程。
/// </summary>
public static class PageRouter
{
    private static ContentControl? _host;

    public static Control? CurrentPage => _host?.Content as Control;

    public static void Attach(ContentControl host)
    {
        _host = host;
    }

    public static void Navigate(Control page)
    {
        Navigate(() => page);
    }

    public static void Navigate(string key, object? parameter = null)
    {
        Navigate(() => _CreateFromRegistry(key, parameter));
    }

    public static void Navigate(Func<Control?> pageFactory)
    {
        if (_host is null) return;

        if (Dispatcher.UIThread.CheckAccess()) _Show(pageFactory);
        else Dispatcher.UIThread.Post(() => _Show(pageFactory));
    }

    private static Control? _CreateFromRegistry(string key, object? parameter)
    {
        if (!PageRegistry.TryCreate(key, out var page))
        {
            LogWrapper.Warn("UI", $"未注册的页面键：{key}");
            return null;
        }

        (page as IPageView)?.OnNavigatedTo(parameter);
        return page;
    }

    private static void _Show(Func<Control?> pageFactory)
    {
        var host = _host;
        if (host is null) return;

        (host.Content as IPageView)?.OnNavigatedFrom();
        var page = pageFactory();
        if (page is null) return;

        host.Content = page;
        _ = AnimationService.FadeInAsync(page);
    }
}
