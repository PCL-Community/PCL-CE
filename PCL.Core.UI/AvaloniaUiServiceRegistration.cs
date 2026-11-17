using Microsoft.Extensions.DependencyInjection;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>Avalonia UI 服务注册。</summary>
public static class AvaloniaUiServiceRegistration
{
    public static IServiceCollection AddAvaloniaUi(
        this IServiceCollection services,
        PlatformKind kind)
    {
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IFileDialogService>(_ => kind switch
        {
            PlatformKind.Windows or PlatformKind.MacOs => new FileDialogService(),
            _ => new LinuxFileDialogService()
        });
        services.AddSingleton<IResourceStore, ResourceStore>();
        services.AddSingleton<ILocalizationBackend>(provider =>
            new LocalizationBackend(provider.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IThemeBackend, ThemeBackend>();
        services.AddSingleton<ISystemThemeProvider>(_ => kind switch
        {
            PlatformKind.Windows or PlatformKind.MacOs => new SystemThemeProvider(),
            _ => new LinuxSystemThemeProvider()
        });
        services.AddSingleton<ISystemNotificationService>(_ => kind switch
        {
            PlatformKind.Windows => new WindowsNotificationService(),
            PlatformKind.Linux => new LinuxNotificationService(),
            _ => new MacOsNotificationService()
        });
        return services;
    }
}