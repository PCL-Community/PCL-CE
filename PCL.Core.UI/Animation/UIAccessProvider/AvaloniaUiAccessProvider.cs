using Avalonia.Threading;
using PCL.Core.UI.Animation.Clock;

namespace PCL.Core.UI.Animation.UIAccessProvider;

/// <summary>
///     基于 Avalonia <see cref="Dispatcher.UIThread" /> 的实现。
/// </summary>
public sealed class AvaloniaUiAccessProvider(Dispatcher? dispatcher = null) : IUiAccessProvider
{
    private readonly Dispatcher _dispatcher = dispatcher ?? Dispatcher.UIThread;

    private readonly AvaloniaRenderingClock _frameClock = new(int.MaxValue);
    private EventHandler? _frameTick;

    public bool CheckAccess()
    {
        return _dispatcher.CheckAccess();
    }

    public void Invoke(Action action)
    {
        if (CheckAccess())
            action();
        else
            _dispatcher.Invoke(action, DispatcherPriority.Send);
    }

    public Task InvokeAsync(Action action)
    {
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var completion =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.Post(
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
            DispatcherPriority.Send);
        return completion.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (CheckAccess())
            return Task.FromResult(func());

        var completion =
            new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.Post(
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
            DispatcherPriority.Send);
        return completion.Task;
    }

    public Task InvokeAsync(Func<Task> func)
    {
        if (CheckAccess())
            return func();

        var completion =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.Post(
            async () =>
            {
                try
                {
                    await func();
                    completion.SetResult();
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            },
            DispatcherPriority.Send);
        return completion.Task;
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        if (CheckAccess())
            return func();

        var completion =
            new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dispatcher.Post(
            async () =>
            {
                try
                {
                    completion.SetResult(await func());
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            },
            DispatcherPriority.Send);
        return completion.Task;
    }

    public event EventHandler FrameTick
    {
        add
        {
            if (_frameTick is null)
            {
                _frameClock.Tick += _OnFrameClockTick;
                _frameClock.Start();
            }

            _frameTick += value;
        }
        remove
        {
            _frameTick -= value;
            if (_frameTick is not null) return;

            _frameClock.Tick -= _OnFrameClockTick;
            _frameClock.Stop();
        }
    }

    private void _OnFrameClockTick(object? sender, long frameIndex)
    {
        _frameTick?.Invoke(this, EventArgs.Empty);
    }
}