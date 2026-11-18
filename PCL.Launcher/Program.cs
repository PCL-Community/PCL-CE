using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Navigation;
using PCL.UI;
using PCL.Views;

namespace PCL;

internal static class Program
{
    /// <summary>程序启动点。</summary>
    [STAThread]
    public static void Main(string[] args)
    {
        var platform = PlatformDetector.Detect();

        PlatformStartup.Initialize(platform, args);

        var applicationServices = PlatformRegistration.Register(platform);
        var serviceProvider = LauncherServiceRegistration.Build(applicationServices);

        if (StartupCommandHandler.TryHandle(args, serviceProvider))
            return;

        var builder = PlatformStartup.Configure(platform);
        PageRegistry.Initialize(serviceProvider.GetRequiredService<PageHostFactory>());

        AnimationRegistration.Register();

        ApplicationService.Loading = () =>
            new ApplicationHost(() => builder.StartWithClassicDesktopLifetime(args));

        MainWindowService.Loading = () =>
        {
            var window = new MainWindow();
            var context = new WindowContext(window);
            window.InitializeContext(context);
            return context.Host;
        };

        Lifecycle.OnInitialize();
    }

    /// <summary>界面配置入口，同时供可视化设计器使用。</summary>
    public static AppBuilder BuildAvaloniaApp()
    {
        return PlatformStartup.Configure(PlatformDetector.Detect());
    }
}