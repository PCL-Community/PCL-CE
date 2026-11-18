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
        services.AddSingleton<IFileDialogService>(provider => kind switch
        {
            PlatformKind.Windows or PlatformKind.MacOs => new FileDialogService(),
            _ => new LinuxFileDialogService(
                provider.GetRequiredService<IExternalCommandRunner>())
        });
        services.AddSingleton<IResourceStore, ResourceStore>();
        services.AddSingleton<ILocalizationBackend>(provider =>
            new LocalizationBackend(provider.GetRequiredService<IUiDispatcher>()));
        services.AddSingleton<IThemeBackend, ThemeBackend>();
        services.AddSingleton<ISystemThemeProvider>(provider => kind switch
        {
            PlatformKind.Windows or PlatformKind.MacOs => new SystemThemeProvider(),
            _ => new LinuxSystemThemeProvider(
                provider.GetRequiredService<IExternalCommandRunner>())
        });
        services.AddSingleton<ISystemNotificationService>(provider => kind switch
        {
            PlatformKind.Windows => new WindowsNotificationService(
                provider.GetRequiredService<IExternalCommandRunner>()),
            PlatformKind.Linux => new LinuxNotificationService(
                provider.GetRequiredService<IExternalCommandRunner>()),
            _ => new MacOsNotificationService(
                provider.GetRequiredService<IExternalCommandRunner>())
        });
        return services;
    }
}