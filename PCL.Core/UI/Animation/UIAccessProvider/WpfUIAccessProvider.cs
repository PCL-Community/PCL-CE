using System;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace PCL.Core.UI.Animation.UIAccessProvider;

public sealed class WpfUIAccessProvider(Dispatcher dispatcher) : IUIAccessProvider
{
    private readonly Dispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    public bool CheckAccess() => _dispatcher.CheckAccess();

    public void Invoke(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.Invoke(action, DispatcherPriority.Send);
    }

    public Task InvokeAsync(Action action)
    {
        if (!_dispatcher.CheckAccess()) return _dispatcher.InvokeAsync(action, DispatcherPriority.Send).Task;
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        return _dispatcher.CheckAccess() ? Task.FromResult(func()) : _dispatcher.InvokeAsync(func, DispatcherPriority.Send).Task;
    }

    public Task InvokeAsync(Func<Task> func)
    {
        return _dispatcher.CheckAccess() ? func() : _dispatcher.InvokeAsync(func, DispatcherPriority.Send).Task.Unwrap();
    }

    public Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        return _dispatcher.CheckAccess() ? func() : _dispatcher.InvokeAsync(func, DispatcherPriority.Send).Task.Unwrap();
    }
    
    public event EventHandler FrameTick
    {
        add => Invoke(() => CompositionTarget.Rendering += value);
        remove => Invoke(() => CompositionTarget.Rendering -= value);
    }
}