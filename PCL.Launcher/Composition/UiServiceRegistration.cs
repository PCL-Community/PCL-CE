using Microsoft.Extensions.DependencyInjection;
using PCL.UI;
using PCL.UI.Bridges;

namespace PCL.Composition;

/// <summary>把业务层 UI 契约注册到界面实现。</summary>
public static class UiServiceRegistration
{
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