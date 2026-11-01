using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.App.Region;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.UI.Hints;

namespace PCL.Avalonia.UI.Seams;

/// <summary>加载框架通知：任务列表/下载按钮尚未迁移，暂仅记录日志。</summary>
public sealed class AvaloniaLoadingUi : ILoadingUi
{
    public void TaskAdded(Loading.LoaderBase loader)
    {
        UiSeamInteraction.LogMissing(nameof(TaskAdded));
    }

    public void TaskRemoved(Loading.LoaderBase loader)
    {
        UiSeamInteraction.LogMissing(nameof(TaskRemoved));
    }

    public void TaskRefreshed(Loading.LoaderBase loader)
    {
        UiSeamInteraction.LogMissing(nameof(TaskRefreshed));
    }

    public void RefreshDownloadButton()
    {
        UiSeamInteraction.LogMissing(nameof(RefreshDownloadButton));
    }

    public void UpdateButtonProgress(double progress)
    {
        UiSeamInteraction.LogMissing(nameof(UpdateButtonProgress));
    }

    public void UpdateTaskbar(TaskbarProgressState state, double progress)
    {
        // 任务栏进度依赖各平台窗口 API，Avalonia 尚无对应封装
    }
}

/// <summary>本地资源（Comp）页面尚未迁移。</summary>
public sealed class AvaloniaCompLocalUi : ICompLocalUi
{
    public void OnJijResolved()
    {
        UiSeamInteraction.LogMissing(nameof(OnJijResolved));
    }

    public bool IsUpdating(string compPath)
    {
        return false;
    }

    public void RefreshModList()
    {
        UiSeamInteraction.LogMissing(nameof(RefreshModList));
    }

    public void SetLoadProgress(object? page, bool visible)
    {
        UiSeamInteraction.LogMissing(nameof(SetLoadProgress));
    }

    public void SetLoadText(object? page, string key)
    {
        UiSeamInteraction.LogMissing(nameof(SetLoadText));
    }

    public void UpdateOnly(object? page)
    {
        UiSeamInteraction.LogMissing(nameof(UpdateOnly));
    }

    public string? GetCurrentFolderPath(object? page)
    {
        return null;
    }
}

/// <summary>整合包安装页尚未迁移，安装加载器暂不创建。</summary>
public sealed class AvaloniaModpackUi : IModpackUi
{
    public List<Loading.LoaderBase> CreateInstallLoader(object request)
    {
        UiSeamInteraction.LogMissing(nameof(CreateInstallLoader));
        return [];
    }

    public void OnInstallStateChanged(object loader)
    {
        UiSeamInteraction.LogMissing(nameof(OnInstallStateChanged));
    }

    public void AddFolder(string folderPath, string displayName, bool showHint)
    {
        UiSeamInteraction.LogMissing(nameof(AddFolder));
    }
}

/// <summary>登录档案相关页面尚未迁移。</summary>
public sealed class AvaloniaProfileUi : IProfileUi
{
    public object? GetLoginData()
    {
        return null;
    }

    public string IsProfileValid()
    {
        return "";
    }

    public Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(
        DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        return Task.FromResult<AuthorizeResult?>(null);
    }

    public bool HandleMicrosoftXstsError(IdentityModelAuthenticationException exception)
    {
        return false;
    }

    public bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException exception)
    {
        return false;
    }

    public bool HandleMicrosoftCreateProfileError(IdentityModelAuthenticationException exception)
    {
        return false;
    }
}

/// <summary>业务层日志打码：与 WPF 端使用同一套规则。</summary>
public sealed class AvaloniaLogFilter : ILogFilter
{
    public string FilterUserName(string raw, char filterChar)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile) || !raw.Contains(userProfile)) return raw;
        var userName = Path.GetFileName(userProfile);
        if (string.IsNullOrEmpty(userName)) return raw;
        return raw.Replace(
            userProfile,
            userProfile.Replace(userName, new string(filterChar, userName.Length)));
    }

    public string FilterAccessToken(string raw, char filterChar)
    {
        const string marker = "accessToken ";
        var index = raw.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var start = index + marker.Length;
            var end = raw.IndexOf(' ', start);
            if (end < 0) end = raw.Length;
            var length = end - start;
            if (length > 10)
            {
                var token = raw[start..end];
                raw = raw[..start]
                    + token[..5]
                    + new string(filterChar, length - 10)
                    + token[^5..]
                    + raw[end..];
                end = start + length;
            }

            index = raw.IndexOf(marker, end, StringComparison.Ordinal);
        }

        return raw;
    }
}

/// <summary>区域限制功能：与 WPF 端一致，限制判定交给 RegionPolicy。</summary>
public sealed class AvaloniaRestrictedFeatures : IRestrictedFeatures
{
    public bool IsRestricted => _Assess().IsRestricted;

    public bool Check(string feature)
    {
        var assessment = _Assess();
        if (!assessment.IsRestricted) return true;

        LauncherCommon.Log($"[RegionPolicy] 已阻止功能 {feature}，判定原因：{assessment.Reason}");
        HintService.Show(
            Hints.Error(
                Lang.Text(
                    assessment.Classification == RegionClassification.Unknown
                        ? "Region.Feature.Unknown"
                        : "Region.Feature.Restricted")));
        return false;
    }

    public void ThrowIfRestricted(string feature)
    {
        if (!Check(feature)) throw new CancelledException();
    }

    private static RegionAssessment _Assess()
    {
        return RegionPolicy.Evaluate(RegionPolicy.ReadWindowsSignals());
    }
}

/// <summary>实例内存设置：与 WPF 端 PageInstanceSetup/PageSetupLaunch 的取值规则一致。</summary>
public sealed class AvaloniaMemorySettings : IMemorySettings
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;

    public double GetRam(McInstance version, bool? is32BitJava = null)
    {
        var instancePath = version?.PathInstance;
        var ramGive = Config.Instance.MemorySolution[instancePath] switch
        {
            2 => _GetGlobalRam(version),
            1 => _GetRamFromTick(Config.Instance.CustomMemorySize[instancePath]),
            _ => _GetAutoRam(version)
        };

        var is32Bit = is32BitJava ?? !JavaRuntime.IsGameSet64BitJava(version);
        if (is32Bit) ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    public double? GetInitialRam(McInstance version, bool? is32BitJava = null)
    {
        if (Config.Launch.LockMemory) return null;
        if (Config.Launch.MemoryAllocationMode != 1) return null;
        if (Config.Instance.MemorySolution[version?.PathInstance] == 2) return null;

        var initialTick = Config.Instance.CustomInitialMemorySize[version?.PathInstance];
        if (initialTick <= 0) return null;
        return Math.Min(_GetRamFromTick(initialTick), GetRam(version, is32BitJava));
    }

    private static double _GetGlobalRam(McInstance version)
    {
        return Config.Launch.MemoryAllocationMode == 0
            ? _GetAutoRam(version)
            : _GetRamFromTick(Config.Launch.CustomMemorySize);
    }

    private static double _GetAutoRam(McInstance version)
    {
        var ramAvailable = Math.Round(
                GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / BytesPerGigabyte * 10)
            / 10;
        double ramMinimum;
        double ramTarget1;
        double ramTarget2;
        double ramTarget3;
        if (version is not null && !version.IsLoaded) version.Load();
        if (version is not null && version.Modable)
        {
            var modDirectory = new DirectoryInfo(Path.Combine(version.PathIndie, "mods"));
            var modCount = modDirectory.Exists ? modDirectory.GetFiles().Length : 0;
            ramMinimum = 0.5d + modCount / 150d;
            ramTarget1 = 1.5d + modCount / 90d;
            ramTarget2 = 2.7d + modCount / 50d;
            ramTarget3 = 4.5d + modCount / 25d;
        }
        else if (version is not null && version.Info.HasOptiFine)
        {
            ramMinimum = 0.5d;
            ramTarget1 = 1.5d;
            ramTarget2 = 3d;
            ramTarget3 = 5d;
        }
        else
        {
            ramMinimum = 0.5d;
            ramTarget1 = 1.5d;
            ramTarget2 = 2.5d;
            ramTarget3 = 4d;
        }

        var ramGive = 0d;
        var stages = new[]
        {
            (Delta: ramTarget1, Ratio: 1d),
            (Delta: ramTarget2 - ramTarget1, Ratio: 0.7d),
            (Delta: ramTarget3 - ramTarget2, Ratio: 0.4d),
            (Delta: ramTarget3, Ratio: 0.15d)
        };
        foreach (var (delta, ratio) in stages)
        {
            ramGive += Math.Min(ramAvailable * ratio, delta);
            ramAvailable -= delta / ratio;
            if (ramAvailable < 0.1d) break;
        }

        return Math.Round(Math.Max(ramGive, ramMinimum), 1);
    }

    private static double _GetRamFromTick(int value)
    {
        return value switch
        {
            <= 12 => value * 0.1d + 0.3d,
            <= 25 => (value - 12) * 0.5d + 1.5d,
            <= 33 => (value - 25) * 1 + 8,
            _ => (value - 33) * 2 + 16
        };
    }
}

/// <summary>崩溃分析器仍在 WPF 工具包中，Avalonia 侧先记录日志。</summary>
public sealed class AvaloniaCrashAnalysisService : ICrashAnalysisService
{
    public void Analyze(
        int pid,
        McInstance version,
        string pathIndie,
        IReadOnlyList<string> logs,
        IReadOnlyList<string> outputPaths)
    {
        UiSeamInteraction.LogMissing(nameof(Analyze));
    }
}

/// <summary>Comp 详情/任务管理页尚未迁移。</summary>
public sealed class AvaloniaCompNavigation : ICompNavigationService
{
    public void ShowCompDetail(object project)
    {
        UiSeamInteraction.LogMissing(nameof(ShowCompDetail));
    }

    public void ShowTaskManager()
    {
        UiSeamInteraction.LogMissing(nameof(ShowTaskManager));
    }

    public void ShowInstanceSelect()
    {
        UiSeamInteraction.LogMissing(nameof(ShowInstanceSelect));
    }
}

/// <summary>自定义事件涉及的页面操作尚未迁移。</summary>
public sealed class AvaloniaEventUi : IEventUi
{
    public bool RefreshPage()
    {
        return false;
    }

    public void ShowDailyFortune()
    {
        UiSeamInteraction.LogMissing(nameof(ShowDailyFortune));
    }

    public void ClearTrash()
    {
        UiSeamInteraction.LogMissing(nameof(ClearTrash));
    }

    public void SwitchPage(string pageType, string? subType)
    {
        UiSeamInteraction.LogMissing(nameof(SwitchPage));
    }

    public void StartModpackInstall()
    {
        UiSeamInteraction.LogMissing(nameof(StartModpackInstall));
    }

    public void StartCustomDownload(string url, string savePath, string? fileName)
    {
        UiSeamInteraction.LogMissing(nameof(StartCustomDownload));
    }

    public void OpenHelp(string location)
    {
        UiSeamInteraction.LogMissing(nameof(OpenHelp));
    }
}

/// <summary>游戏启动前后的音乐/视频控制尚未迁移。</summary>
public sealed class AvaloniaMediaService : IMediaService
{
    public void PauseMusic()
    {
    }

    public void ResumeMusic()
    {
    }

    public void SetVideoGaming(bool gaming)
    {
    }
}

/// <summary>下载过程的客户端更新提示尚未迁移到 Avalonia 下载页。</summary>
public sealed class AvaloniaDownloadInteraction : IDownloadUserInteraction
{
    public void ShowClientUpdateHint(string versionName, JsonObject json)
    {
        UiSeamInteraction.LogMissing(nameof(ShowClientUpdateHint));
    }
}

/// <summary>把全部业务层 seam 注册到 Avalonia 实现。</summary>
public static class UiSeamRegistration
{
    public static void Register()
    {
        LoadingUi.Current = new AvaloniaLoadingUi();
        LauncherUi.Interaction = new AvaloniaUserInteraction();
        DownloadUserInteraction.Current = new AvaloniaDownloadInteraction();
        UpdateUi.Current = new AvaloniaUpdateUi();
        AnnouncementActions.Current = new AvaloniaAnnouncementActions();
        WatcherUi.Current = new AvaloniaWatcherUi();
        MediaService.Current = new AvaloniaMediaService();
        CrashAnalysisService.Current = new AvaloniaCrashAnalysisService();
        CompNavigation.Current = new AvaloniaCompNavigation();
        CompUi.Current = new AvaloniaCompUi();
        CompLocalUi.Current = new AvaloniaCompLocalUi();
        MemorySettings.Current = new AvaloniaMemorySettings();
        LaunchUi.Current = new AvaloniaLaunchUi();
        LoginProfile.Current = new AvaloniaProfileUi();
        LogFilter.Current = new AvaloniaLogFilter();
        RestrictedFeatures.Current = new AvaloniaRestrictedFeatures();
        ModpackUi.Current = new AvaloniaModpackUi();
        EventUi.Current = new AvaloniaEventUi();
    }
}