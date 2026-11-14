using Avalonia;
using Avalonia.Controls;
using PCL.UI.SystemIntegration;

namespace PCL;

internal interface IPlatformShellIntegration
{
    void OnFrameworkInitialized(Application application);
    void OnWindowCreated(Window window);
}

internal sealed class WindowsShellIntegration : IPlatformShellIntegration
{
    public void OnFrameworkInitialized(Application application)
    {
    }

    public void OnWindowCreated(Window window)
    {
    }
}

internal sealed class LinuxShellIntegration : IPlatformShellIntegration
{
    public void OnFrameworkInitialized(Application application)
    {
    }

    public void OnWindowCreated(Window window)
    {
    }
}

internal sealed class MacOsShellIntegration : IPlatformShellIntegration
{
    public void OnFrameworkInitialized(Application application)
    {
        MacOsDockIcon.Apply();
        MacOsNativeMenu.Install(application);
    }

    public void OnWindowCreated(Window window)
    {
        MacOsNativeMenu.AttachWindow(window);
    }
}