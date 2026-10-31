using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF <see cref="Application.Current" /> 的 UI 调度实现。</summary>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    public bool CheckAccess()
    {
        return Application.Current?.Dispatcher.CheckAccess() ?? false;
    }

    public void Post(Action action)
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

    public void Invoke(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || Lifecycle.HasShutdownStarted) return;

        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.Invoke(action);
    }

    public T? Invoke<T>(Func<T> func)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || Lifecycle.HasShutdownStarted) return default;

        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
    }

    public Task InvokeAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || Lifecycle.HasShutdownStarted) return Task.CompletedTask;

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

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || Lifecycle.HasShutdownStarted) return Task.FromResult(default(T)!);

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