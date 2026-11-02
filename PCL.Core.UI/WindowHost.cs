using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>Avalonia <see cref="Window" /> 的 <see cref="IWindowHost" /> 包装。</summary>
public sealed class WindowHost : IWindowHost
{
    private bool _isLoadedRaised;

    public WindowHost(Window window)
    {
        Window = window;
        Window.Opened += (_, _) => _RaiseLoaded();
    }

    public Window Window { get; }

    public event Action? Loaded;

    private void _RaiseLoaded()
    {
        if (_isLoadedRaised) return;
        _isLoadedRaised = true;
        Loaded?.Invoke();
    }

    public void Show()
    {
        // 启动阶段主窗体由 Avalonia 生命周期的 ShowMainWindow 统一显示，避免重复 Show
        if (DesktopLifetime is { MainWindow: { } mainWindow, Windows.Count: 0 }
            && ReferenceEquals(mainWindow, Window)
            && !Window.IsVisible)
            return;

        if (!Window.IsVisible) Window.Show();
    }

    public void Activate()
    {
        Window.Activate();
    }

    public void Restore()
    {
        if (Window.WindowState == WindowState.Minimized) Window.WindowState = WindowState.Normal;
    }

    public bool IsMinimized => Window.WindowState == WindowState.Minimized;

    public bool IsTopmost
    {
        get => Window.Topmost;
        set => Window.Topmost = value;
    }

    private static IClassicDesktopStyleApplicationLifetime? DesktopLifetime =>
        Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
}