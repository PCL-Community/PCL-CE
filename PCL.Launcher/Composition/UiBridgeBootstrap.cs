using Microsoft.Extensions.DependencyInjection;
using PCL.UI.Bridges;
using PCL.Views.Shell;

namespace PCL.Composition;

/// <summary>临时兼容入口：业务层仍通过静态门面获取 UI 服务时，由 DI 解析唯一实现并回填。 后续业务层改为构造注入后应删除本类。</summary>
public static class UiBridgeBootstrap
{
    public static void Initialize(IServiceProvider services)
    {
        LoadingUi.Current = services.GetRequiredService<ILoadingUi>();
        LauncherUi.Interaction = services.GetRequiredService<IUserInteraction>();
        DownloadUserInteraction.Current = services.GetRequiredService<IDownloadUserInteraction>();
        UpdateUi.Current = services.GetRequiredService<IUpdateUi>();
        AnnouncementActions.Current = services.GetRequiredService<IAnnouncementActionHandler>();
        WatcherUi.Current = services.GetRequiredService<IWatcherUi>();

        var media = services.GetRequiredService<MediaServiceBridge>();
        MediaService.Current = media;
        ExtraButtonsPanel.MusicStateProvider = media.GetMusicButtonState;
        ExtraButtonsPanel.MusicToggleAction = media.ToggleMusic;
        ExtraButtonsPanel.MusicNextAction = media.PlayNextMusic;

        CrashAnalysisService.Current = services.GetRequiredService<ICrashAnalysisService>();
        CompNavigation.Current = services.GetRequiredService<ICompNavigationService>();
        CompUi.Current = services.GetRequiredService<ICompUi>();
        CompLocalUi.Current = services.GetRequiredService<ICompLocalUi>();
        MemorySettings.Current = services.GetRequiredService<IMemorySettings>();
        LaunchUi.Current = services.GetRequiredService<ILaunchUi>();
        LoginProfile.Current = services.GetRequiredService<IProfileUi>();
        LogFilter.Current = services.GetRequiredService<ILogFilter>();
        RestrictedFeatures.Current = services.GetRequiredService<IRestrictedFeatures>();
        ModpackUi.Current = services.GetRequiredService<IModpackUi>();
        EventUi.Current = services.GetRequiredService<IEventUi>();
    }
}