using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>基于 Avalonia <see cref="Dispatcher.UIThread" /> 的 UI 调度实现。</summary>
public sealed class UiDispatcher : IUiDispatcher
{
    public bool CheckAccess()
    {
        return Dispatcher.UIThread.CheckAccess();
    }

    public void Post(Action action)
    {
        if (Lifecycle.HasShutdownStarted) return;

        // IUiDispatcher.Post 的契约是排队而不是执行；在 UI 线程上也必须排队，避免同步重入。
        Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
    }

    public void Invoke(Action action)
    {
        if (Lifecycle.HasShutdownStarted) return;

        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Invoke(action);
    }

    public T? Invoke<T>(Func<T> func)
    {
        if (Lifecycle.HasShutdownStarted) return default;

        return Dispatcher.UIThread.CheckAccess() ? func() : Dispatcher.UIThread.Invoke(func);
    }

    public Task InvokeAsync(Action action)
    {
        if (Lifecycle.HasShutdownStarted) return Task.CompletedTask;

        if (Dispatcher.UIThread.CheckAccess())
            try
            {
                action();
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(
            () =>
            {
                try
                {
                    action();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            },
            DispatcherPriority.Normal);
        return completion.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (Lifecycle.HasShutdownStarted) return Task.FromResult(default(T)!);

        if (Dispatcher.UIThread.CheckAccess())
            try
            {
                return Task.FromResult(func());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(
            () =>
            {
                try
                {
                    completion.SetResult(func());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            },
            DispatcherPriority.Normal);
        return completion.Task;
    }

    public IUiModalFrame CreateModalFrame()
    {
        if (Lifecycle.HasShutdownStarted || !Dispatcher.UIThread.CheckAccess())
            return new NoopModalFrame();
        return new ModalFrame();
    }
}