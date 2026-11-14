using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.App.Region;
using PCL.Core.Logging;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Core.Utils;
using PCL.Media;
using PCL.Navigation;
using PCL.UI.Controls;
using PCL.UI.SystemIntegration;
using PCL.Views.Pages;
using PCL.Views.Pages.Select;
using PCL.Views.Pages.Speed;
using PCL.Views.Pages.Tools;
using PCL.Views.Shell;

namespace PCL.UI.Bridges;

/// <summary>加载框架通知：下载按钮、任务栏进度与任务管理页事件。</summary>
public sealed class LoadingUiBridge : ILoadingUi
{
    private static bool _taskHostWarned;

    public void TaskAdded(Loading.LoaderBase loader)
    {
        NotifyTaskPage(_ => PageSpeedLeft.OnTaskAdded(loader), nameof(TaskAdded));
        RefreshDownloadButtonDelayed();
    }

    public void TaskRemoved(Loading.LoaderBase loader)
    {
        NotifyTaskPage(_ => PageSpeedLeft.OnTaskRemoved(loader), nameof(TaskRemoved));
        RefreshDownloadButtonDelayed();
    }

    public void TaskRefreshed(Loading.LoaderBase loader)
    {
        NotifyTaskPage(_ => PageSpeedLeft.OnTaskRefreshed(loader), nameof(TaskRefreshed));
        RefreshDownloadButtonDelayed();
    }

    public void RefreshDownloadButton()
    {
        ExtraButtonsPanel.Current?.RefreshDownloadButton();
    }

    public void UpdateButtonProgress(double progress)
    {
        ExtraButtonsPanel.Current?.UpdateButtonProgress(progress);
    }

    public void UpdateTaskbar(TaskbarProgressState state, double progress)
    {
        TaskbarProgress.Update(state, progress);
    }

    /// <summary>任务列表事件在业务线程触发，统一投递到 UI 线程，确保页面读到最新的任务栏列表。</summary>
    private static void NotifyTaskPage(Action<PageSpeedLeft> action, string feature)
    {
        var host = PageSpeedLeft.Current;
        if (host is null)
        {
            if (_taskHostWarned) return;
            _taskHostWarned = true;
            UiInteraction.WarnMissing(feature, "TASK");
            return;
        }

        Dispatcher.UIThread.Post(() => action(host));
    }

    private static void RefreshDownloadButtonDelayed()
    {
        Dispatcher.UIThread.Post(() => { ExtraButtonsPanel.Current?.RefreshDownloadButton(); });
    }
}

/// <summary>本地资源（Comp）加载与页面状态的桥接。</summary>
public sealed class CompLocalUiBridge : ICompLocalUi
{
    public void OnJijResolved()
    {
        LauncherCommon.RunInUi(() =>
        {
            if (PageRouter.CurrentPage is ICompLocalPage page) page.RefreshModList();
            else UiInteraction.WarnMissing(nameof(OnJijResolved), "COMPLOCAL");
        });
    }

    public bool IsUpdating(string compPath)
    {
        return CompLocalUpdateRegistry.IsUpdating(compPath);
    }

    public void RefreshModList()
    {
        LauncherCommon.RunInUi(() =>
        {
            if (PageRouter.CurrentPage is ICompLocalPage page) page.RefreshModList();
            else UiInteraction.WarnMissing(nameof(RefreshModList), "COMPLOCAL");
        });
    }

    public void SetLoadProgress(object? page, bool visible)
    {
        if (page is not ICompLocalPage localPage)
        {
            UiInteraction.WarnMissing(nameof(SetLoadProgress), "COMPLOCAL");
            return;
        }

        UiInteraction.RunOnUi(() => localPage.LoadProgressVisible = visible);
    }

    public void SetLoadText(object? page, string key)
    {
        if (page is not ICompLocalPage localPage)
        {
            UiInteraction.WarnMissing(nameof(SetLoadText), "COMPLOCAL");
            return;
        }

        UiInteraction.RunOnUi(() => localPage.SetLoadText(key));
    }

    public void UpdateOnly(object? page)
    {
        if (page is not ICompLocalPage localPage)
        {
            UiInteraction.WarnMissing(nameof(UpdateOnly), "COMPLOCAL");
            return;
        }

        UiInteraction.RunOnUi(localPage.UpdateOnly);
    }

    public string? GetCurrentFolderPath(object? page)
    {
        if (page is ICompLocalPage localPage) return localPage.CurrentFolderPath;
        UiInteraction.WarnMissing(nameof(GetCurrentFolderPath), "COMPLOCAL");
        return null;
    }
}

/// <summary>整合包安装与文件夹列表的桥接。</summary>
public sealed class ModpackUiBridge : IModpackUi
{
    public List<Loading.LoaderBase> CreateInstallLoader(McInstallRequest request)
    {
        return McInstallPipeline.CreateInstallLoader(request);
    }

    public void OnInstallStateChanged(Loading.LoaderBase loader)
    {
        var combo = loader as Loading.LoaderCombo;
        switch (loader.State)
        {
            case LoadState.Finished:
                HandleInstallFinished(loader, combo);
                UiInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Success")}",
                    NotificationType.Success);
                break;

            case LoadState.Failed:
                UiInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Failure")}{loader.Error?.Message}",
                    NotificationType.Error);
                break;

            case LoadState.Aborted:
                UiInteraction.Hint(
                    $"{loader.DisplayName}{Lang.Text("Common.Status.Cancelled")}",
                    NotificationType.Info);
                break;

            case LoadState.Loading:
                return;

            case LoadState.Waiting:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(loader));
        }

        McInstallRecovery.HandleTerminalState(loader, combo?.input?.ToString() ?? "");
        var folder = GameFolders.mcFolderSelected;
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var root = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;
        Loading.LoaderFolderRun(
            InstanceList.mcInstanceListLoader,
            root,
            Loading.LoaderFolderRunType.ForceRun,
            1,
            $"versions{Path.DirectorySeparatorChar}");
    }

    public void AddFolder(string folderPath, string displayName, bool showHint)
    {
        PageSelectLeft.AddFolder(folderPath, displayName, showHint);
    }

    private static void HandleInstallFinished(Loading.LoaderBase loader, Loading.LoaderCombo? combo)
    {
        var instanceFolder = combo?.input?.ToString() ?? "";
        var instanceName = Path.GetFileName(Path.TrimEndingDirectorySeparator(instanceFolder));
        var mcFolder = GameFolders.mcFolderSelected;
        if (string.IsNullOrEmpty(mcFolder) || string.IsNullOrEmpty(instanceName))
            return;
        var pclIni = Path.Combine(mcFolder, "PCL.ini");
        if (Config.Download.AutoSelectInstance)
            LauncherCommon.WriteIni(pclIni, "Version", instanceName);
        LauncherCommon.WriteIni(pclIni, "InstanceCache", "");
    }
}

/// <summary>登录档案相关的界面桥接：档案校验、设备码登录与微软登录错误处理。</summary>
public sealed class ProfileUiBridge : IProfileUi
{
    public GameLaunch.McLoginData? GetLoginData()
    {
        var profile = ProfileService.Current;
        if (profile is null) return new GameLaunch.McLoginLegacy();
        return profile.ProfileType switch
        {
            ProfileType.Microsoft => new GameLaunch.McLoginMs(),
            ProfileType.Authlib => new GameLaunch.McLoginServer(GameLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                UserName = profile.LoginName,
                Password = profile.Password,
                Description = profile.ServerName ?? "Authlib-Injector",
                IsExist = true
            },
            ProfileType.YggdrasilConnect => new GameLaunch.McLoginServer(
                GameLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                Description = profile.ServerName ?? "Yggdrasil Connect",
                IsExist = true,
                ProviderType = ProfileType.YggdrasilConnect,
                DiscoveryAddress = profile.DiscoveryAddress
            },
            _ => new GameLaunch.McLoginLegacy
            {
                UserName = profile.UserName,
                Uuid = profile.Uuid
            }
        };
    }

    public string IsProfileValid()
    {
        var profile = ProfileService.Current;
        if (profile is null) return Lang.Text("Minecraft.Launch.Precheck.NoProfile");
        if (profile.ProfileType != ProfileType.Offline) return string.Empty;
        if (string.IsNullOrWhiteSpace(profile.UserName))
            return Lang.Text("Launch.Account.Profile.Validation.EmptyUsername");
        if (profile.UserName.Contains('"'))
            return Lang.Text("Launch.Account.Profile.Validation.QuoteInUsername");
        if (InstanceList.McMcInstanceSelected is { } instance
            && instance.Info.Drop >= 203
            && profile.UserName.Trim().Length > 16)
            return Lang.Text("Launch.Account.Profile.Validation.UsernameTooLong");
        return string.Empty;
    }

    public Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(
        DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<AuthorizeResult?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var request = new LoginDialogRequest
        {
            Title = Lang.Text("Launch.Account.LoginDialog.MinecraftLogin"),
            Buttons = default,
            Data = JsonSerializer.SerializeToNode(context.Data, JsonCompat.SerializerOptions)!
                .AsObject(),
            DeviceCodePoll = (_, pollToken) => context.PollAsync(pollToken),
            LoginResultHandler = (oauth, _) =>
            {
                completion.TrySetResult(oauth);
                return Task.CompletedTask;
            },
            CompletionHandler = result =>
            {
                if (result is Exception exception) completion.TrySetException(exception);
            }
        };
        DialogService.Instance.Show(request);
        return completion.Task;
    }

    public bool HandleMicrosoftXstsError(IdentityModelAuthenticationException exception)
    {
        if (exception.Error is not { } error
            || !error.StartsWith("xsts_", StringComparison.OrdinalIgnoreCase))
            return false;

        var normalizedError = error.ToLowerInvariant();
        if (normalizedError is not ("xsts_2148916227"
            or "xsts_2148916233"
            or "xsts_2148916235"
            or "xsts_2148916238"))
            return false;

        LauncherCommon.RunInUiWait(() =>
        {
            switch (normalizedError)
            {
                case "xsts_2148916227":
                    _ = DialogService.Instance.ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Minecraft.Launch.Login.Failed"),
                            Content = Lang.Text("Minecraft.Launch.Login.Microsoft.Banned"),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(Lang.Text("Minecraft.Launch.Login.IKnow"))),
                            IsWarn = true
                        });
                    break;
                case "xsts_2148916233":
                    if (UiInteraction.Message(
                            Lang.Text("Minecraft.Launch.Login.Microsoft.XboxNotRegistered"),
                            Lang.Text("Minecraft.Launch.Login.Hint"),
                            Lang.Text("Minecraft.Launch.Login.Register"),
                            Lang.Text("Common.Action.Cancel"))
                        == 1)
                        UiInteraction.OpenWebsite("https://signup.live.com/signup");
                    break;
                case "xsts_2148916235":
                    _ = DialogService.Instance.ShowAsync(
                        new TextDialogRequest
                        {
                            Title = Lang.Text("Minecraft.Launch.Login.Failed"),
                            Content = Lang.Text("Minecraft.Launch.Login.Microsoft.RegionBlocked"),
                            Buttons = DialogButtons.From(
                                DialogButton.Create(Lang.Text("Minecraft.Launch.Login.IKnow")))
                        });
                    break;
                case "xsts_2148916238":
                    if (UiInteraction.Message(
                            Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.Message"),
                            Lang.Text("Minecraft.Launch.Login.Hint"),
                            Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeOver13"),
                            Lang.Text("Minecraft.Launch.Login.Microsoft.Underage.AgeUnder13"),
                            Lang.Text("Common.Option.IDontKnow"))
                        == 1)
                    {
                        UiInteraction.OpenWebsite("https://account.live.com/editprof.aspx");
                        _ = DialogService.Instance.ShowAsync(
                            new TextDialogRequest
                            {
                                Title = Lang.Text("Minecraft.Launch.Login.Hint"),
                                Content = Lang.Text(
                                    "Minecraft.Launch.Login.Microsoft.ChangeBirthDate.Message"),
                                Buttons = DialogButtons.Ok
                            });
                    }
                    else
                    {
                        UiInteraction.OpenWebsite(
                            "https://support.microsoft.com/zh-cn/account-billing/"
                            + "如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a");
                        _ = DialogService.Instance.ShowAsync(
                            new TextDialogRequest
                            {
                                Title = Lang.Text("Minecraft.Launch.Login.Hint"),
                                Content = Lang.Text(
                                    "Minecraft.Launch.Login.Microsoft.ChangeBirthDate.SupportMessage"),
                                Buttons = DialogButtons.Ok
                            });
                    }

                    break;
            }
        });
        return true;
    }

    public bool HandleMicrosoftNotOwnedError(IdentityModelAuthenticationException exception)
    {
        if (!string.Equals(
                exception.Error,
                "minecraft_not_owned",
                StringComparison.OrdinalIgnoreCase))
            return false;

        LauncherCommon.RunInUiWait(() =>
        {
            if (UiInteraction.Message(
                    Lang.Text("Minecraft.Launch.Login.Microsoft.NotPurchased"),
                    Lang.Text("Minecraft.Launch.Login.Failed"),
                    Lang.Text("Minecraft.Launch.Login.Microsoft.PurchaseMinecraft"),
                    Lang.Text("Common.Action.Cancel"))
                == 1)
                UiInteraction.OpenWebsite(
                    "https://www.xbox.com/zh-cn/games/store/"
                    + "minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
        });
        return true;
    }

    public bool HandleMicrosoftCreateProfileError(
        IdentityModelAuthenticationException exception)
    {
        if (!string.Equals(
                exception.Error,
                "profile_not_created",
                StringComparison.OrdinalIgnoreCase))
            return false;

        LauncherCommon.RunInUiWait(() =>
        {
            if (UiInteraction.Message(
                    Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Message"),
                    Lang.Text("Minecraft.Launch.Login.Failed"),
                    Lang.Text("Minecraft.Launch.Login.Microsoft.CreateProfile.Button"),
                    Lang.Text("Common.Action.Cancel"))
                == 1)
                UiInteraction.OpenWebsite(
                    "https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
        });
        return true;
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
#if DEBUG || DEBUGCI
        return new RegionAssessment(RegionClassification.Other, "debug-build");
#else
        return RegionPolicy.Evaluate(RegionPolicy.ReadWindowsSignals());
#endif
    }
}

/// <summary>实例内存设置：按实例级或全局内存方案换算分配值与初始堆，32 位 Java 下上限 1 GB。</summary>
public sealed class MemorySettingsBridge : IMemorySettings
{
    private const double BytesPerGigabyte = 1024d * 1024d * 1024d;

    public double GetRam(McInstance? version, bool? is32BitJava = null)
    {
        var instancePath = version?.PathInstance;
        double ramGive;
        if (string.IsNullOrEmpty(instancePath))
        {
            LogWrapper.Debug("UI", "内存设置缺少实例上下文，使用全局内存配置");
            ramGive = Config.Launch.MemoryAllocationMode == 0
                ? _GetAutoRam(null)
                : _GetRamFromTick(Config.Launch.CustomMemorySize);
        }
        else
        {
            ramGive = Config.Instance.MemorySolution[instancePath] switch
            {
                2 => _GetGlobalRam(version),
                1 => _GetRamFromTick(Config.Instance.CustomMemorySize[instancePath]),
                _ => _GetAutoRam(version)
            };
        }

        var is32Bit = is32BitJava ?? !JavaRuntime.IsGameSet64BitJava(version);
        if (is32Bit) ramGive = Math.Min(1d, ramGive);
        return ramGive;
    }

    public double? GetInitialRam(McInstance? version, bool? is32BitJava = null)
    {
        // 锁定内存或 JVM 参数自带 -Xms 时不再给出初始堆
        if (Config.Launch.LockMemory) return null;
        if (_HasCustomXms(version)) return null;

        var instancePath = version?.PathInstance;
        if (string.IsNullOrEmpty(instancePath))
        {
            LogWrapper.Debug("UI", "初始内存设置缺少实例上下文，使用全局内存配置");
            return _GetGlobalInitialRam(null, is32BitJava);
        }

        return Config.Instance.MemorySolution[instancePath] switch
        {
            2 => _GetGlobalInitialRam(version, is32BitJava),
            1 => _GetInstanceInitialRam(version, is32BitJava),
            _ => null
        };
    }

    /// <summary>全局内存方案（MemorySolution == 2）的初始堆：使用全局设置，仅在手动分配模式下生效。</summary>
    private double? _GetGlobalInitialRam(McInstance? version, bool? is32BitJava)
    {
        if (Config.Launch.MemoryAllocationMode != 1) return null;
        var initialTick = Config.Launch.CustomInitialMemorySize;
        if (initialTick <= 0) return null;
        return Math.Min(_GetRamFromTick(initialTick), GetRam(version, is32BitJava));
    }

    private double? _GetInstanceInitialRam(McInstance? version, bool? is32BitJava)
    {
        var instancePath = version?.PathInstance;
        if (string.IsNullOrEmpty(instancePath)) return null;
        var initialTick = Config.Instance.CustomInitialMemorySize[instancePath];
        if (initialTick <= 0) return null;
        return Math.Min(_GetRamFromTick(initialTick), GetRam(version, is32BitJava));
    }

    /// <summary>判断 JVM 参数是否自带 -Xms：实例级参数优先，为空时取全局参数。</summary>
    private static bool _HasCustomXms(McInstance? version)
    {
        var instancePath = version?.PathInstance;
        var instanceArgs = string.IsNullOrEmpty(instancePath)
            ? ""
            : Config.Instance.JvmArgs[instancePath];
        var effectiveArgs =
            string.IsNullOrEmpty(instanceArgs) ? Config.Launch.JvmArgs : instanceArgs;
        return !string.IsNullOrEmpty(effectiveArgs)
            && effectiveArgs.Contains("-Xms", StringComparison.OrdinalIgnoreCase);
    }

    private static double _GetGlobalRam(McInstance? version)
    {
        return Config.Launch.MemoryAllocationMode == 0
            ? _GetAutoRam(version)
            : _GetRamFromTick(Config.Launch.CustomMemorySize);
    }

    private static double _GetAutoRam(McInstance? version)
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

/// <summary>崩溃分析：收集日志、分析原因并弹出真实结果弹窗，可导出错误报告。</summary>
public sealed class CrashAnalysisBridge : ICrashAnalysisService
{
    public void Analyze(
        int pid,
        McInstance version,
        string pathIndie,
        IReadOnlyList<string> logs,
        IReadOnlyList<string> outputPaths)
    {
        try
        {
            // 稍等游戏进程收尾，保证崩溃日志已落盘
            Thread.Sleep(2000);
            var runner = new CrashRunner(pid);
            runner.Collect(pathIndie, [.. logs]);
            runner.Prepare();
            runner.Analyze(version);
            runner.Output(false, [.. outputPaths]);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "[Watcher] 崩溃分析失败");
        }
    }
}

/// <summary>Comp 详情与二级页面的导航桥接。</summary>
public sealed class CompNavigationBridge : ICompNavigationService
{
    public void ShowCompDetail(Comp.CompProject project)
    {
        PageRouter.Navigate(PageType.CompDetail, PageSubType.Default, project);
    }

    public void ShowTaskManager()
    {
        PageRouter.Navigate(PageType.TaskManager);
    }

    public void ShowInstanceSelect()
    {
        PageRouter.Navigate(PageType.InstanceSelect);
    }
}

/// <summary>自定义事件涉及的页面/工具操作。</summary>
public sealed class EventUiBridge : IEventUi
{
    public bool RefreshPage()
    {
        var refreshed = false;
        LauncherCommon.RunInUiWait(() =>
        {
            if (PageRouter.CurrentPage is not IRefreshable refreshable) return;
            refreshable.Refresh();
            refreshed = true;
        });
        return refreshed;
    }

    public void ShowDailyFortune()
    {
        PageToolsTest.ShowDailyFortune();
    }

    public void ClearTrash()
    {
        PageToolsTest.ClearTrash();
    }

    public void SwitchPage(string pageType, string? subType)
    {
        if (!Enum.TryParse<PageType>(pageType, true, out var page))
        {
            LogWrapper.Warn("UI", $"自定义事件切换页面失败：无法识别页面类型 {pageType}");
            return;
        }

        var sub = PageSubType.Default;
        if (!string.IsNullOrEmpty(subType)
            && !Enum.TryParse(subType, true, out sub))
        {
            LogWrapper.Warn("UI", $"自定义事件切换页面失败：无法识别子页面类型 {subType}");
            return;
        }

        PageRouter.Navigate(page, sub);
    }

    public void StartModpackInstall()
    {
        Modpack.ModpackInstall();
    }

    public void StartCustomDownload(string url, string savePath, string? fileName)
    {
        PageToolsTest.StartCustomDownload(url, fileName, savePath);
    }

    public void OpenHelp(string location)
    {
        try
        {
            var title = PageHelpDetail.ReadTitle(location);
            PageRouter.Navigate(
                new PageStackData(
                    PageType.HelpDetail,
                    PageSubType.Default,
                    location,
                    string.IsNullOrEmpty(title) ? null : title));
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Event.Error.ExecutionFailed", "OpenHelp", location),
                LauncherLogLevel.Msgbox,
                userSummary: Lang.Text("Event.Error.ExecutionFailed", "OpenHelp", location));
        }
    }
}

/// <summary>游戏启动前后的音乐控制，以及设置页与右下角音乐按钮所需的媒体操作。</summary>
public sealed class MediaServiceBridge : IMediaService
{
    private readonly MediaPlaybackService _media = new();

    public bool IsAvailable => _media.IsAvailable;

    public bool IsMusicAvailable => _media.IsMusicAvailable;

    public MusicPlaybackState MusicState => _media.MusicState;

    public int MusicCount => _media.MusicCount;

    public string? CurrentMusicPath => _media.CurrentMusicPath;

    public IReadOnlyList<string> MusicList => _media.MusicList;

    public void PauseMusic()
    {
        _media.PauseMusic();
    }

    public void ResumeMusic()
    {
        _media.ResumeMusic();
    }

    public void ToggleMusic()
    {
        _media.ToggleMusic();
    }

    public void PlayNextMusic()
    {
        _media.PlayNextMusic();
    }

    public void RefreshMusic(bool showHint, bool isFirstLoad = false)
    {
        _media.RefreshMusic(showHint, isFirstLoad);
    }

    public void ClearMusic()
    {
        _media.ClearMusic();
    }

    public void SetMusicVolume(int volume)
    {
        _media.SetMusicVolume(volume);
    }

    /// <summary>右下角音乐快捷按钮的展示状态；没有音乐时返回 <see langword="null" />。</summary>
    public ExtraButtonsPanel.MusicButtonState? GetMusicButtonState()
    {
        if (!_media.IsMusicVisible) return null;

        var playing = _media.MusicState == MusicPlaybackState.Play;
        var paused = _media.MusicState == MusicPlaybackState.Pause;
        var single = _media.MusicCount == 1;
        var name = Path.GetFileNameWithoutExtension(_media.CurrentMusicPath ?? string.Empty);
        var tipKey = paused
            ? single
                ? "Music.ToolTip.Paused.Single"
                : "Music.ToolTip.Paused.Multiple"
            : single
                ? "Music.ToolTip.Playing.Single"
                : "Music.ToolTip.Playing.Multiple";
        return new ExtraButtonsPanel.MusicButtonState(
            true,
            playing,
            Lang.Text(tipKey, name));
    }
}

/// <summary>下载过程的客户端更新提示：消息框 + 跳转下载页 + 打开更新日志。</summary>
public sealed class DownloadInteractionBridge : IDownloadUserInteraction
{
    public void ShowClientUpdateHint(string versionName, JsonObject json)
    {
        try
        {
            JsonNode? version = null;
            foreach (var token in json["versions"]?.AsArray() ?? [])
                if (token?["id"] is not null && token["id"]?.ToString() == versionName)
                {
                    version = token;
                    break;
                }

            if (version is null) return;
            var time = version["releaseTime"]?.ToObject<DateTime>() ?? DateTime.Now;
            var elapsed = DateTime.Now - time;
            var message =
                $"{Lang.Text("Minecraft.Update.NewVersion", versionName)}\r\n"
                + (elapsed.TotalDays > 1d
                    ? Lang.Text("Minecraft.Update.UpdateTime") + Lang.Date(time)
                    : Lang.Text("Minecraft.Update.UpdatedAt")
                    + Lang.TimeSpan(time - DateTime.Now));
            var result = UiInteraction.Message(
                message,
                Lang.Text("Minecraft.Update.Title"),
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Download"),
                (DateTime.Now - time).TotalHours > 3d
                    ? Lang.Text("Common.Action.UpdateLog")
                    : "",
                button3Action: () => OpenUpdateLog(version));
            if (result == 2)
                PageRouter.Navigate(
                    PageType.Download,
                    PageSubType.DownloadInstall,
                    version as JsonObject ?? (object)versionName);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Error.UpdateNotify", versionName),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Error.UpdateNotify", versionName));
        }
    }

    private static void OpenUpdateLog(JsonNode version)
    {
        var wikiName = McFormatter.GetWikiUrlSuffix(version["id"]?.ToString() ?? "");
        var wikiUrl = $"{McFormatter.GetWikiBaseUrl()}/{wikiName.TrimStart('/')}";
        UiInteraction.OpenWebsite(wikiUrl);
    }
}

/// <summary>把全部业务层 UI 平台桥注册到界面实现。</summary>
public static class UiBridgeRegistration
{
    public static void Register()
    {
        LoadingUi.Current = new LoadingUiBridge();
        LauncherUi.Interaction = new UserInteractionBridge();
        DownloadUserInteraction.Current = new DownloadInteractionBridge();
        UpdateUi.Current = new UpdateUiBridge();
        AnnouncementActions.Current = new AnnouncementActionsBridge();
        WatcherUi.Current = new WatcherUiBridge();
        var mediaBridge = new MediaServiceBridge();
        MediaService.Current = mediaBridge;
        ExtraButtonsPanel.MusicStateProvider = mediaBridge.GetMusicButtonState;
        ExtraButtonsPanel.MusicToggleAction = mediaBridge.ToggleMusic;
        ExtraButtonsPanel.MusicNextAction = mediaBridge.PlayNextMusic;
        CrashAnalysisService.Current = new CrashAnalysisBridge();
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