using System.Windows.Threading;
using PCL.Core.App;

namespace PCL;

/// <summary>
///     PCL2 UI 线程调度兼容层。
/// </summary>
public static class UiThread
{
    private static readonly int InitialThreadId = Environment.CurrentManagedThreadId;

    public static bool CheckAccess()
    {
        return System.Windows.Application.Current?.Dispatcher?.CheckAccess()
               ?? Environment.CurrentManagedThreadId == InitialThreadId;
    }

    public static T Invoke<T>(Func<T> action)
    {
        return CheckAccess()
            ? action()
            : System.Windows.Application.Current?.Dispatcher is null
                ? default!
                : System.Windows.Application.Current.Dispatcher.Invoke(action);
    }

    public static void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
            return;
        }

        if (System.Windows.Application.Current?.Dispatcher is null)
            return;
        System.Windows.Application.Current.Dispatcher.Invoke(action);
    }

    public static void Post(Action action, bool forceWaitUntilLoaded = false)
    {
        if (System.Windows.Application.Current?.Dispatcher is null)
            return;
        if (CheckAccess() && !forceWaitUntilLoaded)
            action();
        else
            System.Windows.Application.Current.Dispatcher.InvokeAsync(
                action,
                forceWaitUntilLoaded
                    ? DispatcherPriority.Loaded
                    : DispatcherPriority.Normal);
    }

    public static void RunInThread(Action action)
    {
        if (CheckAccess())
            Basics.RunInNewThread(action, $"Runtime Invoke {LauncherRuntime.GetUuid()}#");
        else
            action();
    }
}