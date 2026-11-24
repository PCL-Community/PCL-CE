using Avalonia;
using Avalonia.Controls;

namespace PCL;

internal sealed class MacOsPlatformStartup : IPlatformStartup
{
    public IPlatformShellIntegration Shell { get; } = new MacOsShellIntegration();

    public void Initialize(string[] args)
    {
    }

    public AppBuilder Configure()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(
                new MacOSPlatformOptions
                {
                    ShowInDock = true,
                    DisableDefaultApplicationMenuItems = true
                })
            .AfterSetup(builder => NativeMenu.SetMenu(builder.Instance!, new NativeMenu()))
            .WithInterFont()
            .LogToTrace();
    }
}