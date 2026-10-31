using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>WPF <see cref="Application" /> 的 <see cref="IApplicationHost" /> 包装。</summary>
public sealed class WpfApplicationHost : IApplicationHost
{
    private readonly Application _application;
    private IWindowHost? _mainWindow;

    public WpfApplicationHost(Application application)
    {
        _application = application;
        _application.Startup += (_, _) => Startup?.Invoke();
        _application.DispatcherUnhandledException +=
            (_, e) => DispatcherUnhandledException?.Invoke(e.Exception);
    }

    public int Run()
    {
        return _application.Run();
    }

    public IWindowHost? MainWindow
    {
        get => _mainWindow;
        set
        {
            _mainWindow = value;
            if (value is WpfWindowHost windowHost) _application.MainWindow = windowHost.Window;
        }
    }

    public object? TryFindResource(string key)
    {
        return _application.TryFindResource(key);
    }

    public object? FindResource(string key)
    {
        return _application.FindResource(key);
    }

    public void SetResource(object key, object value)
    {
        _application.Resources[key] = value;
    }

    public void Shutdown()
    {
        var dispatcher = _application.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownFinished) return;

        using var exited = new ManualResetEventSlim();
        dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            () =>
            {
                _application.Exit += Exited;
                if (dispatcher.HasShutdownStarted) return;
                _application.Shutdown();
            });

        try
        {
            exited.Wait(5000);
        }
        finally
        {
            dispatcher.BeginInvoke(DispatcherPriority.Send, () => _application.Exit -= Exited);
        }

        return;

        void Exited(object? sender, EventArgs e)
        {
            exited.Set();
        }
    }

    public event Action? Startup;

    public event Action<Exception>? DispatcherUnhandledException;
}