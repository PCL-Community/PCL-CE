using Microsoft.Extensions.DependencyInjection;
using PCL.Core.Platform;
using PCL.UI;
using PCL.UI.Bridges;

namespace PCL.Composition;

/// <summary>把业务层 UI 契约注册到界面实现。</summary>
public static class UiServiceRegistration
{
    private static readonly Type[] _RequiredServices =
    [
        typeof(IUiDispatcher),
        typeof(ILocalizationBackend),
        typeof(IThemeBackend),
        typeof(IClipboardService),
        typeof(IFileDialogService),
        typeof(IResourceStore),
        typeof(ISystemNotificationService),
        typeof(ISystemThemeProvider),
        typeof(ILoadingUi),
        typeof(IUserInteraction),
        typeof(IDownloadUserInteraction),
        typeof(IUpdateUi),
        typeof(IAnnouncementActionHandler),
        typeof(IWatcherUi),
        typeof(IMediaService),
        typeof(ICrashAnalysisService),
        typeof(ICompNavigationService),
        typeof(ICompUi),
        typeof(ICompLocalUi),
        typeof(IMemorySettings),
        typeof(ILaunchUi),
        typeof(IProfileUi),
        typeof(ILogFilter),
        typeof(IRestrictedFeatures),
        typeof(IModpackUi),
        typeof(IEventUi)
    ];

    public static void Validate(IServiceCollection services)
    {
        var missing = _RequiredServices
            .Where(type => services.All(descriptor => descriptor.ServiceType != type))
            .Select(type => type.Name)
            .ToArray();
        if (missing.Length == 0) return;
        throw new InvalidOperationException($"缺少必需的 UI 服务注册：{string.Join(", ", missing)}");
    }

    public static IServiceCollection AddLauncherUi(this IServiceCollection services)
    {
        services.AddSingleton<ILoadingUi, LoadingUiBridge>();
        services.AddSingleton<IUserInteraction, UserInteractionBridge>();
        services.AddSingleton<IDownloadUserInteraction, DownloadInteractionBridge>();
        services.AddSingleton<IUpdateUi, UpdateUiBridge>();
        services.AddSingleton<IAnnouncementActionHandler, AnnouncementActionsBridge>();
        services.AddSingleton<IWatcherUi, WatcherUiBridge>();
        services.AddSingleton<MediaServiceBridge>();
        services.AddSingleton<IMediaService>(provider =>
            provider.GetRequiredService<MediaServiceBridge>());
        services.AddSingleton<ICrashAnalysisService, CrashAnalysisBridge>();
        services.AddSingleton<ICompNavigationService, CompNavigationBridge>();
        services.AddSingleton<ICompUi, CompUiBridge>();
        services.AddSingleton<ICompLocalUi, CompLocalUiBridge>();
        services.AddSingleton<IMemorySettings, MemorySettingsBridge>();
        services.AddSingleton<ILaunchUi, LaunchUiBridge>();
        services.AddSingleton<IProfileUi, ProfileUiBridge>();
        services.AddSingleton<ILogFilter, LogFilterBridge>();
        services.AddSingleton<IRestrictedFeatures, RestrictedFeaturesBridge>();
        services.AddSingleton<IModpackUi, ModpackUiBridge>();
        services.AddSingleton<IEventUi, EventUiBridge>();

        return services;
    }
}