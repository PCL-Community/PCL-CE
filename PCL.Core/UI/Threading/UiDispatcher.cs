using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PCL.Core.App.IoC;

namespace PCL.Core.UI.Threading;

/// <summary>WPF UI 线程访问与调度。</summary>
public static class UiDispatcher
{
    public static bool CheckAccess() => Application.Current?.Dispatcher.CheckAccess() ?? false;

    public static void Post(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return;

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
    }

    /// <summary>在 UI 线程同步执行指定操作，UI 不可用时直接返回。</summary>
    public static void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || Lifecycle.HasShutdownStarted) return;

        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    /// <summary>在 UI 线程同步执行指定操作并返回结果，UI 不可用时返回默认值。</summary>
    public static T? Invoke<T>(Func<T> func)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || Lifecycle.HasShutdownStarted) return default;

        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
    }

    public static Task InvokeAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return Task.CompletedTask;

        if (!dispatcher.CheckAccess())
            return dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;

        try
        {
            action();
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return Task.FromResult(default(T)!);

        if (!dispatcher.CheckAccess())
            return dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task;

        try
        {
            return Task.FromResult(func());
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }
}
