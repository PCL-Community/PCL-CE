using Microsoft.Extensions.DependencyInjection;
using PCL.Core.Platform;
using PCL.Navigation;

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

        var platform = applicationServices.Platform;
        services.AddSingleton(platform.SystemProxy);
        services.AddSingleton(platform.WindowInterop);
        services.AddSingleton(platform.Shortcuts);
        services.AddSingleton(platform.Java);
        services.AddSingleton(platform.ProcessCleanup);
        services.AddSingleton(platform.GpuPreference);
        services.AddSingleton(platform.LaunchEnvironment);
        services.AddSingleton(platform.SystemFacts);
        services.AddSingleton(platform.Hardware);
        services.AddSingleton(platform.Shell);
        services.AddSingleton(platform.Region);
        services.AddSingleton(platform.RecycleBin);
        services.AddSingleton(platform.FatalDialog);

        var ui = applicationServices.Ui;
        services.AddSingleton(ui.Dispatcher);
        services.AddSingleton(ui.Clipboard);
        services.AddSingleton(ui.FileDialogs);
        services.AddSingleton(ui.ResourceStore);
        services.AddSingleton(ui.Localization);
        services.AddSingleton(ui.Theme);
        services.AddSingleton(ui.SystemTheme);
        services.AddSingleton(ui.Notifications);

        services.AddSingleton<INavigationService>(_ => NavigationService.Instance);
        services.AddSingleton(_ => PageCatalog.Snapshot);
        services.AddSingleton<PageHostFactory>();
        services.AddSingleton<PageViewFactory>();

        // Host 的构造依赖。页面本身由 PageViewFactory 创建，不需要逐个注册。
        services.AddTransient<PCL.Views.Pages.Launch.PageLaunchLeft>();
        services.AddTransient<PCL.Views.Pages.Launch.PageLaunchRight>();
        services.AddTransient<PCL.Views.Pages.Download.PageDownloadLeft>();
        services.AddTransient<PCL.Views.Pages.Setup.PageSetupLeft>();
        services.AddTransient<PCL.Views.Pages.Tools.PageToolsLeft>();
        services.AddTransient<PCL.Views.Pages.Instance.PageInstanceLeft>();
        services.AddTransient<PCL.Views.Pages.Instance.PageInstanceSavesLeft>();
        services.AddTransient<PCL.Views.Pages.Select.PageSelectLeft>();
        services.AddTransient<PCL.Views.Pages.Select.PageSelectRight>();
        services.AddTransient<PCL.Views.Pages.Speed.PageSpeedLeft>();
        services.AddTransient<PCL.Views.Pages.Speed.PageSpeedRight>();
        services.AddTransient<PCL.Views.Pages.Log.PageLogLeft>();
        services.AddTransient<PCL.Views.Pages.Log.PageLogRight>();
        services.AddTransient<PCL.Views.Pages.PageHelpDetail>();
        services.AddTransient<PCL.Views.Pages.Download.PageDownloadCompDetail>();
        services.AddTransient<PCL.PageSetupLeftViewModel>();
        services.AddTransient<PCL.PageSetupJavaViewModel>();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
    }
}