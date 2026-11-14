using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     Avalonia <see cref="Application" /> 的 <see cref="IApplicationHost" /> 包装。
///     <see cref="Run" /> 由界面层传入，用于启动 Avalonia 的经典桌面消息循环。
/// </summary>
public sealed class ApplicationHost : IApplicationHost
{
    private readonly Dictionary<object, object> _pendingResources = new();
    private readonly Func<int> _run;
    private Application? _application;
    private IWindowHost? _mainWindow;

    public ApplicationHost(Func<int> run)
    {
        _run = run;
        Current = this;
    }

    /// <summary>当前使用的应用容器，供 Avalonia <c>App</c> 在初始化完成时回填。</summary>
    public static ApplicationHost? Current { get; private set; }

    private IClassicDesktopStyleApplicationLifetime? DesktopLifetime =>
        _application?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;

    public int Run()
    {
        return _run();
    }

    public IWindowHost? MainWindow
    {
        get => _mainWindow;
        set
        {
            _mainWindow = value;
            if (value is WindowHost windowHost && DesktopLifetime is { } lifetime)
                lifetime.MainWindow = windowHost.Window;
        }
    }

    public object? TryFindResource(string key)
    {
        if (_application is not null
            && _application.TryFindResource(key, out var value))
            return value;
        return _pendingResources.GetValueOrDefault(key);
    }

    public object? FindResource(string key)
    {
        if (TryFindResource(key) is { } value) return value;
        throw new KeyNotFoundException($"未找到资源：{key}");
    }

    public void SetResource(object key, object value)
    {
        if (_application is null)
        {
            _pendingResources[key] = value;
            return;
        }

        _application.Resources[key] = value;
    }

    public void Shutdown()
    {
        if (DesktopLifetime is not { } lifetime) return;

        if (Dispatcher.UIThread.CheckAccess())
            lifetime.Shutdown();
        else
            Dispatcher.UIThread.Post(() => lifetime.Shutdown());
    }

    public event Action? Startup;

    public event Action<Exception>? DispatcherUnhandledException;

    /// <summary>由 Avalonia <c>App.OnFrameworkInitializationCompleted</c> 调用，标记应用容器就绪。</summary>
    public static void NotifyStartup(Application application)
    {
        Current?._OnApplicationInitialized(application);
    }

    private void _OnApplicationInitialized(Application application)
    {
        _application = application;

        foreach (var pair in _pendingResources)
            application.Resources[pair.Key] = pair.Value;
        _pendingResources.Clear();

        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            // 生命周期会按致命错误处理该异常并在原生对话框关闭后结束进程。
            // 先标记已处理，避免 Avalonia 在对话框关闭前重抛异常并拆除 Dispatcher。
            e.Handled = true;

            try
            {
                DispatcherUnhandledException?.Invoke(e.Exception);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[UI] 未处理异常处理器失败：{ex}");
                Lifecycle.RequestFatalExit();
            }
        };

        if (PlatformServices.Localization is LocalizationBackend localization)
            localization.EnsureBaseLanguage();

        Startup?.Invoke();
    }
}