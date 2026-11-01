using Avalonia.Controls;
using Avalonia.Threading;

namespace PCL.Avalonia.Navigation;

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

    public static void Navigate(Func<Control> pageFactory)
    {
        var host = _host;
        if (host is null) return;

        if (Dispatcher.UIThread.CheckAccess())
            host.Content = pageFactory();
        else
            Dispatcher.UIThread.Post(() => host.Content = pageFactory());
    }
}