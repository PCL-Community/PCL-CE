using System;
using System.Windows;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>WPF <see cref="Window" /> 的 <see cref="IWindowHost" /> 包装。</summary>
public sealed class WpfWindowHost : IWindowHost
{
    public WpfWindowHost(Window window)
    {
        Window = window;
        Window.Loaded += (_, _) => Loaded?.Invoke();
    }

    public Window Window { get; }

    public event Action? Loaded;

    public void Show()
    {
        Window.Show();
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
}