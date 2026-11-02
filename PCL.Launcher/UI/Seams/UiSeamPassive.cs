using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.App.Region;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.UI.Hints;

namespace PCL.UI.Seams;

/// <summary>加载框架通知：任务列表与下载按钮尚未接入界面，暂仅记录日志。</summary>
public sealed class LoadingUiBridge : ILoadingUi
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
        // 任务栏进度依赖各平台窗口 API，当前没有可用的跨平台封装
    }
}

/// <summary>本地资源（Comp）页面尚未接入界面。</summary>
public sealed class CompLocalUiBridge : ICompLocalUi
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

/// <summary>整合包安装页尚未接入界面，安装加载器暂不创建。</summary>
public sealed class ModpackUiBridge : IModpackUi
{
    public List<Loading.LoaderBase> CreateInstallLoader(McInstallRequest request)
    {
        UiSeamInteraction.LogMissing(nameof(CreateInstallLoader));
        return [];
    }

    public void OnInstallStateChanged(Loading.LoaderBase loader)
    {
        UiSeamInteraction.LogMissing(nameof(OnInstallStateChanged));
    }

    public void AddFolder(string folderPath, string displayName, bool showHint)
    {
        UiSeamInteraction.LogMissing(nameof(AddFolder));
    }
}

/// <summary>登录档案相关页面尚未接入界面。</summary>
public sealed class ProfileUiBridge : IProfileUi
{
    public GameLaunch.McLoginData? GetLoginData()
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

/// <summary>业务层日志打码：遮盖用户目录中的用户名，并把 AccessToken 首尾 5 位之外的部分替换为掩码字符。</summary>
public sealed class LogFilterBridge : ILogFilter
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

        // 打码当前登录结果：UUID 与 AccessToken 相同（离线登录）时没有可保护的信息
        var login = LogFilter.CurrentLogin;
        var accessToken = login?.AccessToken;
        if (accessToken is { Length: >= 10 }
            && raw.Contains(accessToken, StringComparison.OrdinalIgnoreCase)
            && (login?.Uuid ?? "") != accessToken)
            raw = raw.Replace(
                accessToken,
                accessToken[..5]
                + new string(filterChar, accessToken.Length - 10)
                + accessToken[^5..]);

        return raw;
    }
}

/// <summary>区域限制功能：判定交给 RegionPolicy，命中限制时提示并拒绝调用。</summary>
public sealed class RestrictedFeaturesBridge : IRestrictedFeatures
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

/// <summary>实例内存设置：按实例级或全局内存方案换算分配值与初始堆，32 位 Java 下上限 1 GB。</summary>
public sealed class MemorySettingsBridge : IMemorySettings
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
        // 锁定内存或 JVM 参数自带 -Xms 时不再给出初始堆
        if (Config.Launch.LockMemory) return null;
        if (_HasCustomXms(version)) return null;

        var instancePath = version?.PathInstance;
        return Config.Instance.MemorySolution[instancePath] switch
        {
            2 => _GetGlobalInitialRam(version, is32BitJava),
            1 => _GetInstanceInitialRam(version, is32BitJava),
            _ => null
        };
    }

    /// <summary>全局内存方案（MemorySolution == 2）的初始堆：使用全局设置，仅在手动分配模式下生效。</summary>
    private double? _GetGlobalInitialRam(McInstance version, bool? is32BitJava)
    {
        if (Config.Launch.MemoryAllocationMode != 1) return null;
        var initialTick = Config.Launch.CustomInitialMemorySize;
        if (initialTick <= 0) return null;
        return Math.Min(_GetRamFromTick(initialTick), GetRam(version, is32BitJava));
    }

    /// <summary>实例级内存方案（MemorySolution == 1）的初始堆。</summary>
    private double? _GetInstanceInitialRam(McInstance version, bool? is32BitJava)
    {
        var initialTick = Config.Instance.CustomInitialMemorySize[version?.PathInstance];
        if (initialTick <= 0) return null;
        return Math.Min(_GetRamFromTick(initialTick), GetRam(version, is32BitJava));
    }

    /// <summary>判断 JVM 参数是否自带 -Xms：实例级参数优先，为空时取全局参数。</summary>
    private static bool _HasCustomXms(McInstance version)
    {
        var instanceArgs = version is null ? "" : Config.Instance.JvmArgs[version.PathInstance];
        var effectiveArgs =
            string.IsNullOrEmpty(instanceArgs) ? Config.Launch.JvmArgs : instanceArgs;
        return !string.IsNullOrEmpty(effectiveArgs)
            && effectiveArgs.Contains("-Xms", StringComparison.OrdinalIgnoreCase);
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

/// <summary>崩溃分析：当前只记录一条 seam 缺失日志，不产出分析结果。</summary>
public sealed class CrashAnalysisSeam : ICrashAnalysisService
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

/// <summary>Comp 详情与任务管理页尚未接入界面。</summary>
public sealed class CompNavigationBridge : ICompNavigationService
{
    public void ShowCompDetail(Comp.CompProject project)
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

/// <summary>自定义事件涉及的页面操作尚未接入界面。</summary>
public sealed class EventUiBridge : IEventUi
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

/// <summary>游戏启动前后的音乐与视频控制尚未接入界面。</summary>
public sealed class MediaServiceBridge : IMediaService
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

/// <summary>下载过程的客户端更新提示尚未接到界面下载页。</summary>
public sealed class DownloadInteractionBridge : IDownloadUserInteraction
{
    public void ShowClientUpdateHint(string versionName, JsonObject json)
    {
        UiSeamInteraction.LogMissing(nameof(ShowClientUpdateHint));
    }
}

/// <summary>把全部业务层 seam 注册到界面实现。</summary>
public static class UiSeamRegistration
{
    public static void Register()
    {
        LoadingUi.Current = new LoadingUiBridge();
        LauncherUi.Interaction = new UserInteractionBridge();
        DownloadUserInteraction.Current = new DownloadInteractionBridge();
        UpdateUi.Current = new UpdateUiBridge();
        AnnouncementActions.Current = new AnnouncementActionsBridge();
        WatcherUi.Current = new WatcherUiBridge();
        MediaService.Current = new MediaServiceBridge();
        CrashAnalysisService.Current = new CrashAnalysisSeam();
        CompNavigation.Current = new CompNavigationBridge();
        CompUi.Current = new CompUiBridge();
        CompLocalUi.Current = new CompLocalUiBridge();
        MemorySettings.Current = new MemorySettingsBridge();
        LaunchUi.Current = new LaunchUiBridge();
        LoginProfile.Current = new ProfileUiBridge();
        LogFilter.Current = new LogFilterBridge();
        RestrictedFeatures.Current = new RestrictedFeaturesBridge();
        ModpackUi.Current = new ModpackUiBridge();
        EventUi.Current = new EventUiBridge();
    }
}