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
            if (ModBase.versionBaseName.Contains("beta"))
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
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.versionBaseName),
                    ModBase.versionCode);
                var isBetaLatest = remoteServer.IsLatest(UpdateChannel.beta,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.versionBaseName),
                    ModBase.versionCode);
                return isNewerThanStable && isBetaLatest
                    ? UpdateEnums.VersionStatus.Latest
                    : UpdateEnums.VersionStatus.NotLatest;
            }

            return remoteServer.IsLatest(
                IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.versionBaseName),
                ModBase.versionCode)
                ? UpdateEnums.VersionStatus.Latest
                : UpdateEnums.VersionStatus.NotLatest;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Update.Check.Failed"), ModBase.LogLevel.Hint);
            return UpdateEnums.VersionStatus.Unknown;
        }
    }
    
    public static ModLoader.LoaderCombo<JsonObject> updateLoader;

    public static void UpdateStart(UpdateEnums.UpdateType type, string receivedKey = null, bool forceValidated = false)
    {
        var dlTargetPath = ModBase.exePath + @"PCL\Plain Craft Launcher Community Edition.exe";
        ModBase.RunInNewThread(() =>
        {
            try
            {
                var version = remoteServer.GetLatestVersion(
                    IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64
                );

                ModBase.WriteFile($"{ModBase.pathTemp}CEUpdateLog.md", version.Changelog);
                ModBase.Log($"[Update] 远程最新版本: {version.VersionName}, 当前版本: {ModBase.versionBaseName}");
                if (!(SemVer.Parse(version.VersionName) > SemVer.Parse(ModBase.versionBaseName)))
                    return;
                if (type == UpdateEnums.UpdateType.PromptOnly)
                {
                    ModBase.RunInUi(() =>
                    {
                        if (ModMain.MyMsgBox(
                                Lang.Text("Update.Available", ModBase.versionBaseName, version.VersionName),
                                Lang.Text("Update.Title"),
                                Lang.Text("Update.Action"),
                                Lang.Text("Common.Action.Cancel")
                            ) == 1)
                            ModMain.frmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);
                    });
                    return;
                    // 构造步骤加载器
                }

                var loaders = new List<ModLoader.LoaderBase>();
                // 下载
                loaders.AddRange(remoteServer.GetDownloadLoader(
                    IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, dlTargetPath));
                loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Update.Task.Check"), _ =>
                {
                    var curHash = ModBase.GetFileSHA256(dlTargetPath);
                    if ((curHash ?? "") != (version.Sha256 ?? ""))
                        throw new Exception(Lang.Text("Update.Error.Sha256Mismatch", version.Sha256, curHash));
                }));
                if (type == UpdateEnums.UpdateType.UpdateNow)
                    loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Update.Task.Install"), _ => UpdateRestart(true)));
                else if (type == UpdateEnums.UpdateType.Silent)
                    loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Update.Task.Prepare"), _ => isUpdateWaitingRestart = true));
                else if (type == UpdateEnums.UpdateType.DownloadAndPrompt)
                    loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Update.Task.ShowButton"), _ =>
                    {
                        isUpdateWaitingRestart = true;
                        ModBase.RunInUi(() =>
                        {
                            ModMain.frmMain.BtnExtraUpdateRestart.ToolTip =
                                Lang.Text("Main.Extra.UpdateRestart.ToolTipWithVersion", ModBase.versionBaseName, version.VersionName);
                            ModMain.frmMain.BtnExtraUpdateRestart.ShowRefresh();
                            ModMain.frmMain.BtnExtraUpdateRestart.Ribble();
                        });
                    })
                    {
                        show = false
                    });
                loaders.Add(new ModLoader.LoaderTask<int, int>(Lang.Text("Update.Task.RefreshSettings"), _ =>
                {
                    if (ModMain.frmSetupUpdate is not null)
                        ModBase.RunInUi(() =>
                        {
                            ModMain.frmSetupUpdate.BtnUpdate.Text = Lang.Text("Update.Task.RestartInstall");
                            ModMain.frmSetupUpdate.BtnUpdate.IsEnabled = true;
                        });
                })
                {
                    show = false
                });
                // 启动
                updateLoader = new ModLoader.LoaderCombo<JsonObject>(Lang.Text("Update.Title"), loaders);
                updateLoader.Start();
                if (type == UpdateEnums.UpdateType.UpdateNow)
                {
                    ModLoader.LoaderTaskbarAdd(updateLoader);
                    ModMain.frmMain.BtnExtraDownload.ShowRefresh();
                    ModMain.frmMain.BtnExtraDownload.Ribble();
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Update] 获取启动器更新失败");
                if (type != UpdateEnums.UpdateType.Silent)
                    ModMain.Hint(Lang.Text("Update.Error.FetchFailed"), ModMain.HintType.Critical);
            }
        });
    }

    public static void UpdateRestart(bool triggerRestartAndByEnd, bool triggerRestart = true)
    {
        try
        {
            var fileName = ModBase.exePath + @"PCL\Plain Craft Launcher Community Edition.exe";
            if (!File.Exists(fileName))
            {
                ModBase.Log("[System] 更新失败：未找到更新文件");
                return;
            }

            // id old new restart
            var text =
                $"update {Process.GetCurrentProcess().Id} \"{Basics.ExecutablePath}\" \"{fileName}\" {(triggerRestart ? "true" : "false")}";
            ModBase.Log("[System] 更新程序启动，参数：" + text);
            Process.Start(new ProcessStartInfo(fileName)
                { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = text });
            if (triggerRestartAndByEnd)
            {
                ModMain.frmMain.EndProgram(false, true);
                ModBase.Log("[System] 已由于更新强制结束程序");
            }
        }
        catch (Win32Exception ex)
        {
            ModBase.Log(ex, "自动更新时触发 Win32 错误，疑似被拦截");
            if (ModMain.MyMsgBox(Lang.Text("Update.Error.UpdateBlockedMessage", ModBase.exePath),
                    Lang.Text("Update.Error.UpdateBlocked"),
                    Lang.Text("Update.Error.ViewHelp"),
                    Lang.Text("Common.Action.Confirm"),
                    "", true
                ) == 1)
                CustomEvent.Raise(CustomEvent.EventType.打开帮助, "启动器/Microsoft Defender 添加排除项.json");
        }
    }

    /// <summary>
    ///     确保 PathTemp 下的 Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ///     如果不是，则下载一个。
    /// </summary>
    internal static void DownloadLatestPCL(ModLoader.LoaderBase loaderToSyncProgress = null)
    {
        // 注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
        var latestPCLPath = Path.Combine(ModBase.pathTemp, "CE-Latest.exe");
        var target = remoteServer.GetLatestVersion(UpdateChannel.stable,
            SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64);
        if (target is null)
            throw new Exception(Lang.Text("Update.Error.UnableToGetUpdate"));
        if (File.Exists(latestPCLPath) && (ModBase.GetFileSHA256(latestPCLPath) ?? "") == (target.Sha256 ?? ""))
        {
            ModBase.Log("[System] 最新版 PCL 已存在，跳过下载");
            return;
        }

        if ((ModBase.GetFileSHA256(Basics.ExecutablePath) ?? "") == (target.Sha256 ?? "")) // 正在使用的版本符合要求，直接拿来用
        {
            ModBase.CopyFile(Basics.ExecutablePath, latestPCLPath);
            return;
        }

        var loaders = remoteServer.GetDownloadLoader(UpdateChannel.stable,
            SystemInfo.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, latestPCLPath);
        var loader = new ModLoader.LoaderCombo<int>(Lang.Text("Update.Task.DownloadLatestStable"), loaders);
        loader.Start();
        loader.WaitForExit();
    }

    public static ModLoader.LoaderTask<int, int> serverLoader =
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
                ModBase.Log("[Update] 更新设置: 自动下载并安装更新");
                if (GetVersionStatus() != UpdateEnums.VersionStatus.Latest)
                    UpdateStart(UpdateEnums.UpdateType.Silent);
                break;
            case LauncherAutoUpdateBehavior.DownloadAndAnnounce:
                ModBase.Log("[Update] 更新设置: 自动下载并提示更新");
                UpdateStart(UpdateEnums.UpdateType.DownloadAndPrompt);
                break;
            case LauncherAutoUpdateBehavior.AnnounceOnly:
                ModBase.Log("[Update] 更新设置: 提示更新");
                UpdateStart(UpdateEnums.UpdateType.PromptOnly);
                break;
            default:
                ModBase.Log("[Update] 更新设置: 不自动检查更新");
                return;
        }
    }

    /// <summary>
    ///     展示社区版提示
    /// </summary>
    /// <param name="IsUpdate">是否为更新时启动</param>
    public static void ShowCEAnnounce()
    {
        ModMain.MyMsgBox(Lang.Text("Update.CommunityNotice.Body"),
            Lang.Text("Update.CommunityNotice.Title"),
            Lang.Text("Update.CommunityNotice.Confirm"));
    }
}