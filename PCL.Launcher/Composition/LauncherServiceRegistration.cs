using Microsoft.Extensions.DependencyInjection;
using PCL.Composition;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Navigation;
using PCL.Platform;
using PCL.UI.Controls;
using PCL.UI.SystemIntegration;
using PCL.Views.Pages;
using PCL.Views.Pages.Download;
using PCL.Views.Pages.Instance;
using PCL.Views.Pages.Launch;
using PCL.Views.Pages.Log;
using PCL.Views.Pages.Select;
using PCL.Views.Pages.Setup;
using PCL.Views.Pages.Speed;
using PCL.Views.Pages.Tools;

namespace PCL;

/// <summary>启动器组合根：把平台/UI 服务注册给页面对象图，页面不直接依赖服务容器。</summary>
public static class LauncherServiceRegistration
{
    public static ServiceProvider Build(ApplicationServices applicationServices)
    {
        ArgumentNullException.ThrowIfNull(applicationServices);

        var services = new ServiceCollection();
        services.AddSingleton(applicationServices);
        services.AddSingleton(applicationServices.Platform);
        services.AddSingleton(applicationServices.Ui);

        var kind = applicationServices.Platform.Kind;

        services.AddPlatformServices(kind);
        services.AddAvaloniaUi(kind);
        services.AddSingleton(applicationServices.Platform.Capabilities);

        services.AddSingleton<INavigationService>(_ => NavigationService.Instance);
        services.AddSingleton(_ => PageCatalog.Snapshot);
        services.AddSingleton<PageHostFactory>();
        services.AddSingleton<PageViewFactory>();
        services.AddLauncherUi();
        UiServiceRegistration.Validate(services);

        services.AddSingleton<IUpdatePolicy>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsUpdatePolicy(),
            _ => new ManualUpdatePolicy()
        });
        services.AddSingleton<IPlatformFontProvider>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsFontProvider(),
            PlatformKind.MacOs => new MacOsFontProvider(),
            _ => new LinuxFontProvider()
        });
        services.AddSingleton<ITaskbarProgressBackend>(_ => kind switch
        {
            PlatformKind.Windows => WindowsTaskbarProgress.Instance,
            PlatformKind.Linux => LinuxTaskbarProgress.Instance,
            _ => UnsupportedTaskbarProgressBackend.Instance
        });

        // Host 的构造依赖。页面本身由 PageViewFactory 创建，不需要逐个注册。
        services.AddTransient<PageLaunchLeft>();
        services.AddTransient<PageLaunchRight>();
        services.AddTransient<PageDownloadLeft>();
        services.AddTransient<PageSetupLeft>();
        services.AddTransient<PageToolsLeft>();
        services.AddTransient<PageInstanceLeft>();
        services.AddTransient<PageInstanceSavesLeft>();
        services.AddTransient<PageSelectLeft>();
        services.AddTransient<PageSelectRight>();
        services.AddTransient<PageSpeedLeft>();
        services.AddTransient<PageSpeedRight>();
        services.AddTransient<PageLogLeft>();
        services.AddTransient<PageLogRight>();
        services.AddTransient<PageHelpDetail>();
        services.AddTransient<PageDownloadCompDetail>();
        services.AddTransient<PageSetupLeftViewModel>();
        services.AddTransient<PageSetupJavaViewModel>();

        var provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        applicationServices.Services = provider;
        return provider;
    }
}