using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

public static class UpdateManager
{
    public static bool isUpdateWaitingRestart;

    public static UpdatesWrapperModel remoteServer = new(new List<IUpdateSource>
    {
        new UpdatesMirrorChyanModel(),
        new UpdatesRandomModel(new[]
        {
            new UpdatesMinioModel("https://s3.pysio.online/pcl2-ce/", "Pysio"),
            new UpdatesMinioModel("https://staticassets.naids.com/resources/pclce/", "Naids")
        }),
        new UpdatesMinioModel("https://github.com/PCL-Community/PCL2_CE_Server/raw/main/", "GitHub")
    });

    public static bool IsCurrentVersionBeta
    {
        get
        {
            if (Common.VersionBaseName.Contains("beta"))
                return true;
            return (int)Config.Update.UpdateChannel == 1;
        }
    }
    
    public static UpdateEnums.VersionStatus GetVersionStatus()
    {
        try
        {
            if (IsCurrentVersionBeta && (int)Config.Update.UpdateChannel != 1)
            {
                var isNewerThanStable = remoteServer.IsLatest(UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64,
                    SemVer.Parse(Common.VersionBaseName),
                    Common.VersionCode);
                var isBetaLatest = remoteServer.IsLatest(UpdateChannel.beta,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64,
                    SemVer.Parse(Common.VersionBaseName),
                    Common.VersionCode);
                return isNewerThanStable && isBetaLatest
                    ? UpdateEnums.VersionStatus.Latest
                    : UpdateEnums.VersionStatus.NotLatest;
            }

            return remoteServer.IsLatest(
                IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64,
                SemVer.Parse(Common.VersionBaseName),
                Common.VersionCode)
                ? UpdateEnums.VersionStatus.Latest
                : UpdateEnums.VersionStatus.NotLatest;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Update.Check.Failed"),
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Update.Check.Failed"));
            return UpdateEnums.VersionStatus.Unknown;
        }
    }
    
    public static Loading.LoaderCombo<JsonObject> updateLoader;

    public static void UpdateStart(UpdateEnums.UpdateType type, string receivedKey = null, bool forceValidated = false)
    {
        var dlTargetPath = Common.ExePath + @"PCL\Plain Craft Launcher Community Edition.exe";
        Common.RunInNewThread(() =>
        {
            try
            {
                var version = remoteServer.GetLatestVersion(
                    IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64
                );

                Common.WriteFile($"{Common.pathTemp}CEUpdateLog.md", version.Changelog);
                Common.Log(
                    $"[Update] 远程最新版本: {version.VersionName}, 当前版本: {Common.VersionBaseName}");
                if (!(SemVer.Parse(version.VersionName) > SemVer.Parse(Common.VersionBaseName)))
                    return;
                if (type == UpdateEnums.UpdateType.PromptOnly)
                {
                    Common.RunInUi(() =>
                    {
                        if (Launcher.MyMsgBox(
                                Lang.Text("Update.Available", Common.VersionBaseName,
                                    version.VersionName),
                                Lang.Text("Update.Title"),
                                Lang.Text("Update.Action"),
                                Lang.Text("Common.Action.Cancel")
                            ) == 1)
                            Launcher.frmMain.PageChange(FormMain.PageType.Setup,
                                FormMain.PageSubType.SetupUpdate);
                    });
                    return;
                    // 构造步骤加载器
                }

                var loaders = new List<Loading.LoaderBase>();
                // 下载
                loaders.AddRange(remoteServer.GetDownloadLoader(
                    IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, dlTargetPath));
                loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Update.Task.Check"), _ =>
                {
                    var curHash = Common.GetFileSha256(dlTargetPath);
                    if ((curHash ?? "") != (version.Sha256 ?? ""))
                        throw new Exception(Lang.Text("Update.Error.Sha256Mismatch", version.Sha256, curHash));
                }));
                if (type == UpdateEnums.UpdateType.UpdateNow)
                    loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Update.Task.Install"),
                        _ => UpdateRestart(true)));
                else if (type == UpdateEnums.UpdateType.Silent)
                    loaders.Add(new Loading.LoaderTask<int, int>(Lang.Text("Update.Task.Prepare"),
                        _ => isUpdateWaitingRestart = true));
                else if (type == UpdateEnums.UpdateType.DownloadAndPrompt)
                    loaders.Add(new Loading.LoaderTask<int, int>(
                        Lang.Text("Update.Task.ShowButton"), _ =>
                    {
                        isUpdateWaitingRestart = true;
                        Common.RunInUi(() =>
                        {
                            LocalizedText.TryBind(
                                Launcher.frmMain.BtnExtraUpdateRestart,
                                "ToolTip",
                                () => Lang.Text(
                                    "Main.Extra.UpdateRestart.ToolTipWithVersion",
                                    Common.VersionBaseName,
                                    version.VersionName));
                            Launcher.frmMain.BtnExtraUpdateRestart.ShowRefresh();
                            Launcher.frmMain.BtnExtraUpdateRestart.Ribble();
                        });
                    })
                    {
                        show = false
                    });
                loaders.Add(new Loading.LoaderTask<int, int>(
                    Lang.Text("Update.Task.RefreshSettings"), _ =>
                {
                    if (Launcher.frmSetupUpdate is not null)
                        Common.RunInUi(() =>
                        {
                            Launcher.frmSetupUpdate.BtnUpdate.TrySetLocalizedValue(
                                "Text",
                                "Update.Task.RestartInstall");
                            Launcher.frmSetupUpdate.BtnUpdate.IsEnabled = true;
                        });
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
                    Launcher.frmMain.BtnExtraDownload.ShowRefresh();
                    Launcher.frmMain.BtnExtraDownload.Ribble();
                }
            }
            catch (Exception ex)
            {
                Common.Log(ex, "[Update] 获取启动器更新失败");
                if (type != UpdateEnums.UpdateType.Silent)
                    HintService.Hint(Lang.Text("Update.Error.FetchFailed"), HintType.Error);
            }
        });
    }

    public static void UpdateRestart(bool triggerRestartAndByEnd, bool triggerRestart = true)
    {
        try
        {
            var fileName = Common.ExePath + @"PCL\Plain Craft Launcher Community Edition.exe";
            if (!File.Exists(fileName))
            {
                Common.Log("[System] 更新失败：未找到更新文件");
                return;
            }

            // id old new restart
            var text =
                $"update {Process.GetCurrentProcess().Id} \"{Basics.ExecutablePath}\" \"{fileName}\" {(triggerRestart ? "true" : "false")}";
            Common.Log("[System] 更新程序启动，参数：" + text);
            Process.Start(new ProcessStartInfo(fileName)
                { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = text });
            if (triggerRestartAndByEnd)
            {
                Launcher.frmMain.EndProgram(false, true);
                Common.Log("[System] 已由于更新强制结束程序");
            }
        }
        catch (Win32Exception ex)
        {
            Common.Log(ex, "自动更新时触发 Win32 错误，疑似被拦截");
            Launcher.MyMsgBox(
                Lang.Text("Update.Error.UpdateBlockedMessage", Common.ExePath),
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
    internal static void DownloadLatestPCL(Loading.LoaderBase loaderToSyncProgress = null)
    {
        // 注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
        var latestPCLPath = Path.Combine(Common.pathTemp, "CE-Latest.exe");
        var target = remoteServer.GetLatestVersion(UpdateChannel.stable,
            SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64);
        if (target is null)
            throw new Exception(Lang.Text("Update.Error.UnableToGetUpdate"));
        if (File.Exists(latestPCLPath) &&
            (Common.GetFileSha256(latestPCLPath) ?? "") == (target.Sha256 ?? ""))
        {
            Common.Log("[System] 最新版 PCL 已存在，跳过下载");
            return;
        }

        if ((Common.GetFileSha256(Basics.ExecutablePath) ?? "") ==
            (target.Sha256 ?? "")) // 正在使用的版本符合要求，直接拿来用
        {
            Common.CopyFile(Basics.ExecutablePath, latestPCLPath);
            return;
        }

        var loaders = remoteServer.GetDownloadLoader(UpdateChannel.stable,
            SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, latestPCLPath);
        var loader =
            new Loading.LoaderCombo<int>(Lang.Text("Update.Task.DownloadLatestStable"), loaders);
        loader.Start();
        loader.WaitForExit();
    }

    public static Loading.LoaderTask<int, int> serverLoader =
        new(Lang.Text("Update.Service.PclCe"),
            _ => LoadOnlineInfo(),
            priority: ThreadPriority.BelowNormal);

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
                Common.Log("[Update] 更新设置: 自动下载并安装更新");
                if (GetVersionStatus() != UpdateEnums.VersionStatus.Latest)
                    UpdateStart(UpdateEnums.UpdateType.Silent);
                break;
            case LauncherAutoUpdateBehavior.DownloadAndAnnounce:
                Common.Log("[Update] 更新设置: 自动下载并提示更新");
                UpdateStart(UpdateEnums.UpdateType.DownloadAndPrompt);
                break;
            case LauncherAutoUpdateBehavior.AnnounceOnly:
                Common.Log("[Update] 更新设置: 提示更新");
                UpdateStart(UpdateEnums.UpdateType.PromptOnly);
                break;
            default:
                Common.Log("[Update] 更新设置: 不自动检查更新");
                return;
        }
    }

    /// <summary>
    ///     展示社区版提示
    /// </summary>
    /// <param name="IsUpdate">是否为更新时启动</param>
    public static void ShowCEAnnounce()
    {
        Launcher.MyMsgBox(Lang.Text("Update.CommunityNotice.Body"),
            Lang.Text("Update.CommunityNotice.Title"),
            Lang.Text("Update.CommunityNotice.Confirm"));
    }
}
