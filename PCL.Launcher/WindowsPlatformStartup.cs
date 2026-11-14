using Avalonia;
using PCL.Core.App;
using PCL.Core.Utils.OS;

namespace PCL;

internal sealed class WindowsPlatformStartup : IPlatformStartup
{
    public IPlatformShellIntegration Shell { get; } = new WindowsShellIntegration();

    public void Initialize(string[] args)
    {
        if (Basics.CommandLineArguments.Contains("--console"))
            KernelInterop.AllocateConsole();
    }

    public AppBuilder Configure()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}