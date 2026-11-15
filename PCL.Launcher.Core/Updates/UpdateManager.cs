using System.ComponentModel;
using System.Diagnostics;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public static class UpdateManager
{
    public static bool isUpdateWaitingRestart;

    public static UpdatesWrapperModel remoteServer = new(
        new List<IUpdateSource>
        {
            new UpdatesMirrorChyanModel(),
            new UpdatesRandomModel(
            [
                new UpdatesMinioModel("https://s3.pysio.online/pcl2-ce/", "Pysio"),
                new UpdatesMinioModel(
                    "https://staticassets.naids.com/resources/pclce/",
                    "Naids")
            ]),
            new UpdatesMinioModel(
                "https://github.com/PCL-Community/PCL2_CE_Server/raw/main/",
                "GitHub")
        });

    public static Loading.LoaderCombo<JsonObject> updateLoader;

    public static Loading.LoaderTask<int, int> serverLoader =
        new(
            Lang.Text("Update.Service.PclCe"),
            _ => LoadOnlineInfo(),
            priority: ThreadPriority.BelowNormal);

    public static bool IsCurrentVersionBeta
    {
        get
        {
            if (LauncherCommon.VersionBaseName.Contains("beta"))
                return true;
            return (int)Config.Update.UpdateChannel == 1;
        }
    }

    public static UpdateEnums.VersionStatus GetVersionStatus()
    {
        try
        {
            if (!IsCurrentVersionBeta || (int)Config.Update.UpdateChannel == 1)
                return remoteServer.IsLatest(
                    IsCurrentVersionBeta
                        ? UpdateChannel.Beta
                        : UpdateChannel.Stable,
                    SystemInfo.IsArm64System
                        ? UpdateArch.Arm64
                        : UpdateArch.X64,
                    SemVer.Parse(LauncherCommon.VersionBaseName),
                    LauncherCommon.VersionCode)
                    ? UpdateEnums.VersionStatus.Latest
                    : UpdateEnums.VersionStatus.NotLatest;

            var isNewerThanStable = remoteServer.IsLatest(
                UpdateChannel.Stable,
                SystemInfo.IsArm64System
                    ? UpdateArch.Arm64
                    : UpdateArch.X64,
                SemVer.Parse(LauncherCommon.VersionBaseName),
                LauncherCommon.VersionCode);
            var isBetaLatest = remoteServer.IsLatest(
                UpdateChannel.Beta,
                SystemInfo.IsArm64System
                    ? UpdateArch.Arm64
                    : UpdateArch.X64,
                SemVer.Parse(LauncherCommon.VersionBaseName),
                LauncherCommon.VersionCode);

            return isNewerThanStable && isBetaLatest
                ? UpdateEnums.VersionStatus.Latest
                : UpdateEnums.VersionStatus.NotLatest;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Update.Check.Failed"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Update.Check.Failed"));
            return UpdateEnums.VersionStatus.Unknown;
        }
    }

    public static void UpdateStart(
        UpdateEnums.UpdateType type,
        string? receivedKey = null,
        bool forceValidated = false)
    {
        var dlTargetPath =
            $@"{LauncherPaths.ExecutableDirectory}PCL\Plain Craft Launcher Community Edition.exe";
        LauncherCommon.RunInNewThread(() =>
        {
            try
            {
                var version = remoteServer.GetLatestVersion(
                    IsCurrentVersionBeta ? UpdateChannel.Beta : UpdateChannel.Stable,
                    SystemInfo.IsArm64System ? UpdateArch.Arm64 : UpdateArch.X64
                );

                LauncherCommon.WriteFile($"{LauncherPaths.Temp}CEUpdateLog.md", version.Changelog);
                LauncherCommon.Log(
                    $"[Update] 远程最新版本: {version.VersionName}, 当前版本: {LauncherCommon.VersionBaseName}");
                if (!(SemVer.Parse(version.VersionName)
                        > SemVer.Parse(LauncherCommon.VersionBaseName)))
                    return;
                if (type == UpdateEnums.UpdateType.PromptOnly)
                {
                    LauncherCommon.RunInUi(() =>
                    {
                        if ((UpdateUi.Current?.ShowMessageBox(
                                    Lang.Text(
                                        "Update.Available",
                                        LauncherCommon.VersionBaseName,
                                        version.VersionName),
                                    Lang.Text("Update.Title"),
                                    Lang.Text("Update.Action"),
                                    Lang.Text("Common.Action.Cancel"))
                                ?? 0)
                            == 1)
                            UpdateUi.Current?.NavigateSetupUpdate();
                    });
                    return;
                    // 构造步骤加载器
                }

                var loaders = new List<Loading.LoaderBase>();
                // 下载
                loaders.AddRange(
                    remoteServer.GetDownloadLoader(
                        IsCurrentVersionBeta ? UpdateChannel.Beta : UpdateChannel.Stable,
                        SystemInfo.IsArm64System ? UpdateArch.Arm64 : UpdateArch.X64,
                        dlTargetPath));
                loaders.Add(
                    new Loading.LoaderTask<int, int>(
                        Lang.Text("Update.Task.Check"),
                        _ =>
                        {
                            var curHash = LauncherCommon.GetFileSha256(dlTargetPath);
                            if ((curHash ?? "") != (version.Sha256 ?? ""))
                                throw new Exception(
                                    Lang.Text(
                                        "Update.Error.Sha256Mismatch",
                                        version.Sha256,
                                        curHash));
                        }));
                switch (type)
                {
                    case UpdateEnums.UpdateType.UpdateNow:
                        loaders.Add(
                            new Loading.LoaderTask<int, int>(
                                Lang.Text("Update.Task.Install"),
                                _ => UpdateRestart(true)));
                        break;

                    case UpdateEnums.UpdateType.Silent:
                        loaders.Add(
                            new Loading.LoaderTask<int, int>(
                                Lang.Text("Update.Task.Prepare"),
                                _ => isUpdateWaitingRestart = true));
                        break;

                    case UpdateEnums.UpdateType.DownloadAndPrompt:
                        loaders.Add(
                            new Loading.LoaderTask<int, int>(
                                Lang.Text("Update.Task.ShowButton"),
                                _ =>
                                {
                                    isUpdateWaitingRestart = true;
                                    UpdateUi.Current?.ShowUpdateRestartButton(
                                        Lang.Text(
                                            "Main.Extra.UpdateRestart.ToolTipWithVersion",
                                            LauncherCommon.VersionBaseName,
                                            version.VersionName));
                                })
                            {
                                show = false
                            });
                        break;

                    case UpdateEnums.UpdateType.PromptOnly:
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), type, null);
                }

                loaders.Add(
                    new Loading.LoaderTask<int, int>(
                        Lang.Text("Update.Task.RefreshSettings"),
                        _ =>
                        {
                            UpdateUi.Current?.UpdateSetupButton(
                                Lang.Text("Update.Task.RestartInstall"),
                                true);
                        })
                    {
                        show = false
                    });
                // 启动
                updateLoader =
                    new Loading.LoaderCombo<JsonObject>(Lang.Text("Update.Title"), loaders);
                updateLoader.Start();
                if (type == UpdateEnums.UpdateType.UpdateNow)
                {
                    Loading.LoaderTaskbarAdd(updateLoader);
                    UpdateUi.Current?.RefreshDownloadButton();
                }
            }
            catch (Exception ex)
            {
                LauncherCommon.Log(ex, "[Update] 获取启动器更新失败");
                if (type != UpdateEnums.UpdateType.Silent)
                    UpdateUi.Current?.ShowHint(
                        Lang.Text("Update.Error.FetchFailed"),
                        NotificationType.Error);
            }
        });
    }

    public static void UpdateRestart(bool triggerRestartAndByEnd, bool triggerRestart = true)
    {
        try
        {
            var fileName = LauncherPaths.ExecutableDirectory
                + @"PCL\Plain Craft Launcher Community Edition.exe";
            if (!File.Exists(fileName))
            {
                LauncherCommon.Log("[System] 更新失败：未找到更新文件");
                return;
            }

            // id old new restart
            var text =
                $"update {Process.GetCurrentProcess().Id} \"{Basics.ExecutablePath}\" \"{fileName}\" {(triggerRestart ? "true" : "false")}";
            LauncherCommon.Log("[System] 更新程序启动，参数：" + text);
            Process.Start(
                new ProcessStartInfo(fileName)
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    Arguments = text
                });
            if (triggerRestartAndByEnd)
            {
                UpdateUi.Current?.ExitApplication(true);
                LauncherCommon.Log("[System] 已由于更新强制结束程序");
            }
        }
        catch (Win32Exception ex)
        {
            LauncherCommon.Log(ex, "自动更新时触发 Win32 错误，疑似被拦截");
            UpdateUi.Current?.ShowMessageBox(
                Lang.Text("Update.Error.UpdateBlockedMessage", LauncherPaths.ExecutableDirectory),
                Lang.Text("Update.Error.UpdateBlocked"),
                Lang.Text("Common.Action.Confirm"),
                "",
                "",
                true);
        }
    }

    /// <summary>
    ///     确保 PathTemp 下的 Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ///     如果不是，则下载一个。
    /// </summary>
    internal static void DownloadLatestPcl(Loading.LoaderBase? loaderToSyncProgress = null)
    {
        // 注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
        var latestPclPath = Path.Combine(LauncherPaths.Temp, "CE-Latest.exe");
        var target = remoteServer.GetLatestVersion(
            UpdateChannel.Stable,
            SystemInfo.IsArm64System ? UpdateArch.Arm64 : UpdateArch.X64);
        if (target is null)
            throw new Exception(Lang.Text("Update.Error.UnableToGetUpdate"));
        if (File.Exists(latestPclPath)
            && (LauncherCommon.GetFileSha256(latestPclPath) ?? "") == (target.Sha256 ?? ""))
        {
            LauncherCommon.Log("[System] 最新版 PCL 已存在，跳过下载");
            return;
        }

        if ((LauncherCommon.GetFileSha256(Basics.ExecutablePath) ?? "")
            == (target.Sha256 ?? "")) // 正在使用的版本符合要求，直接拿来用
        {
            LauncherCommon.CopyFile(Basics.ExecutablePath, latestPclPath);
            return;
        }

        var loaders = remoteServer.GetDownloadLoader(
            UpdateChannel.Stable,
            SystemInfo.IsArm64System ? UpdateArch.Arm64 : UpdateArch.X64,
            latestPclPath);
        var loader =
            new Loading.LoaderCombo<int>(Lang.Text("Update.Task.DownloadLatestStable"), loaders);
        loader.Start();
        loader.WaitForExit();
    }

    private static void LoadOnlineInfo()
    {
        ScheduleBasedOnConfig();
        AnnouncementService.Load();
    }

    private static void ScheduleBasedOnConfig()
    {
        switch (Config.Update.UpdateMode)
        {
            case LauncherAutoUpdateBehavior.DownloadAndInstall:
                LauncherCommon.Log("[Update] 更新设置: 自动下载并安装更新");
                if (GetVersionStatus() != UpdateEnums.VersionStatus.Latest)
                    UpdateStart(UpdateEnums.UpdateType.Silent);
                break;

            case LauncherAutoUpdateBehavior.DownloadAndAnnounce:
                LauncherCommon.Log("[Update] 更新设置: 自动下载并提示更新");
                UpdateStart(UpdateEnums.UpdateType.DownloadAndPrompt);
                break;

            case LauncherAutoUpdateBehavior.AnnounceOnly:
                LauncherCommon.Log("[Update] 更新设置: 提示更新");
                UpdateStart(UpdateEnums.UpdateType.PromptOnly);
                break;

            case LauncherAutoUpdateBehavior.Disable:
            default:
                LauncherCommon.Log("[Update] 更新设置: 不自动检查更新");
                return;
        }
    }

    /// <summary>
    ///     展示社区版提示
    /// </summary>
    /// <param name="IsUpdate">是否为更新时启动</param>
    public static void ShowCeAnnounce()
    {
        UpdateUi.Current?.ShowMessageBox(
            Lang.Text("Update.CommunityNotice.Body"),
            Lang.Text("Update.CommunityNotice.Title"),
            Lang.Text("Update.CommunityNotice.Confirm"));
    }
}