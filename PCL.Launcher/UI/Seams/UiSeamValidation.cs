using PCL.Core.Logging;

namespace PCL.UI.Seams;

/// <summary>业务 UI seam 的启动期校验：缺注册时 Debug/CI 直接失败，Release 记错误日志。</summary>
internal static class UiSeamValidation
{
    internal static void ValidateRequiredSeams()
    {
        var missing = new List<string>();
        Check("LoadingUi", LoadingUi.Current);
        Check("LauncherUi.Interaction", LauncherUi.Interaction);
        Check("DownloadUserInteraction", DownloadUserInteraction.Current);
        Check("UpdateUi", UpdateUi.Current);
        Check("AnnouncementActions", AnnouncementActions.Current);
        Check("WatcherUi", WatcherUi.Current);
        Check("MediaService", MediaService.Current);
        Check("CrashAnalysisService", CrashAnalysisService.Current);
        Check("CompNavigation", CompNavigation.Current);
        Check("CompUi", CompUi.Current);
        Check("CompLocalUi", CompLocalUi.Current);
        Check("MemorySettings", MemorySettings.Current);
        Check("LaunchUi", LaunchUi.Current);
        Check("LoginProfile", LoginProfile.Current);
        Check("LogFilter", LogFilter.Current);
        Check("RestrictedFeatures", RestrictedFeatures.Current);
        Check("ModpackUi", ModpackUi.Current);
        Check("EventUi", EventUi.Current);

        if (missing.Count == 0) return;

        var message = $"业务 UI seam 未注册：{string.Join("、", missing)}";
#if DEBUG
        throw new InvalidOperationException(message);
#else
        LogWrapper.Error("UI", message);
#endif

        void Check(string name, object? instance)
        {
            if (instance is null) missing.Add(name);
        }
    }
}