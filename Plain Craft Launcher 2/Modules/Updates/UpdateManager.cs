using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.Utils;

namespace PCL;

public static class UpdateManager
{
    public static bool IsUpdateWaitingRestart;

    public static UpdatesWrapperModel RemoteServer = new(new List<IUpdateSource>
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
            if (ModBase.VersionBaseName.Contains("beta"))
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
                var isNewerThanStable = RemoteServer.IsLatest(UpdateChannel.stable,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.VersionBaseName),
                    ModBase.VersionCode);
                var isBetaLatest = RemoteServer.IsLatest(UpdateChannel.beta,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.VersionBaseName),
                    ModBase.VersionCode);
                return isNewerThanStable && isBetaLatest
                    ? UpdateEnums.VersionStatus.Latest
                    : UpdateEnums.VersionStatus.NotLatest;
            }

            return RemoteServer.IsLatest(
                IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.VersionBaseName),
                ModBase.VersionCode)
                ? UpdateEnums.VersionStatus.Latest
                : UpdateEnums.VersionStatus.NotLatest;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "无法获取最新版本信息，请检查网络连接", ModBase.LogLevel.Hint);
            return UpdateEnums.VersionStatus.Unknown;
        }
    }
    
    public static ModLoader.LoaderCombo<JObject> UpdateLoader;

    public static void UpdateStart(UpdateEnums.UpdateType type, string receivedKey = null, bool forceValidated = false)
    {
        var dlTargetPath = ModBase.ExePath + @"PCL\Plain Craft Launcher Community Edition.exe";
        ModBase.RunInNewThread(() =>
        {
            try
            {
                var version = RemoteServer.GetLatestVersion(
                    IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64
                );

                ModBase.WriteFile($"{ModBase.PathTemp}CEUpdateLog.md", version.Changelog);
                ModBase.Log($"[Update] 远程最新版本: {version.VersionName}, 当前版本: {ModBase.VersionBaseName}");
                if (!(SemVer.Parse(version.VersionName) > SemVer.Parse(ModBase.VersionBaseName)))
                    return;
                if (type == UpdateEnums.UpdateType.PromptOnly)
                {
                    ModBase.RunInUi(() =>
                    {
                        if (ModMain.MyMsgBox(
                                $"启动器有新版本可用（{ModBase.VersionBaseName} -> {version.VersionName}){"\r\n"}是否立即更新？",
                                "启动器更新", "更新", "取消") ==
                            1) ModMain.FrmMain.PageChange(FormMain.PageType.Setup, FormMain.PageSubType.SetupUpdate);
                    });
                    return;
                    // 构造步骤加载器
                }

                var loaders = new List<ModLoader.LoaderBase>();
                // 下载
                loaders.AddRange(RemoteServer.GetDownloadLoader(
                    IsCurrentVersionBeta ? UpdateChannel.beta : UpdateChannel.stable,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, dlTargetPath));
                loaders.Add(new ModLoader.LoaderTask<int, int>("校验更新", _ =>
                {
                    var curHash = ModBase.GetFileSHA256(dlTargetPath);
                    if ((curHash ?? "") != (version.SHA256 ?? ""))
                        throw new Exception($"更新文件 SHA256 不正确，应该为 {version.SHA256}，实际为 {curHash}");
                }));
                if (type == UpdateEnums.UpdateType.UpdateNow)
                    loaders.Add(new ModLoader.LoaderTask<int, int>("安装更新", _ => UpdateRestart(true)));
                else if (type == UpdateEnums.UpdateType.Silent)
                    loaders.Add(new ModLoader.LoaderTask<int, int>("准备更新", _ => IsUpdateWaitingRestart = true));
                else if (type == UpdateEnums.UpdateType.DownloadAndPrompt)
                    loaders.Add(new ModLoader.LoaderTask<int, int>("显示按钮", _ =>
                    {
                        IsUpdateWaitingRestart = true;
                        ModBase.RunInUi(() =>
                        {
                            ModMain.FrmMain.BtnExtraUpdateRestart.ToolTip =
                                $"重启 PCL CE 以应用软件更新 ({ModBase.VersionBaseName} → {version.VersionName})";
                            ModMain.FrmMain.BtnExtraUpdateRestart.ShowRefresh();
                            ModMain.FrmMain.BtnExtraUpdateRestart.Ribble();
                        });
                    })
                    {
                        Show = false
                    });
                loaders.Add(new ModLoader.LoaderTask<int, int>("刷新设置 UI", _ =>
                {
                    if (ModMain.FrmSetupUpdate is not null)
                        ModBase.RunInUi(() =>
                        {
                            ModMain.FrmSetupUpdate.BtnUpdate.Text = "重启安装";
                            ModMain.FrmSetupUpdate.BtnUpdate.IsEnabled = true;
                        });
                })
                {
                    Show = false
                });
                // 启动
                UpdateLoader = new ModLoader.LoaderCombo<JObject>("启动器更新", loaders);
                UpdateLoader.Start();
                if (type == UpdateEnums.UpdateType.UpdateNow)
                {
                    ModLoader.LoaderTaskbarAdd(UpdateLoader);
                    ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                    ModMain.FrmMain.BtnExtraDownload.Ribble();
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Update] 获取启动器更新失败");
                if (type != UpdateEnums.UpdateType.Silent)
                    ModMain.Hint("获取启动器更新失败，请检查网络连接", ModMain.HintType.Critical);
            }
        });
    }

    public static void UpdateRestart(bool triggerRestartAndByEnd, bool triggerRestart = true)
    {
        try
        {
            var fileName = ModBase.ExePath + @"PCL\Plain Craft Launcher Community Edition.exe";
            if (!File.Exists(fileName))
            {
                ModBase.Log("[System] 更新失败：未找到更新文件");
                return;
            }

            // id old new restart
            var text =
                $"update {Process.GetCurrentProcess().Id} \"{ModBase.ExePathWithName}\" \"{fileName}\" {(triggerRestart ? "true" : "false")}";
            ModBase.Log("[System] 更新程序启动，参数：" + text);
            Process.Start(new ProcessStartInfo(fileName)
                { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true, Arguments = text });
            if (triggerRestartAndByEnd)
            {
                ModMain.FrmMain.EndProgram(false, true);
                ModBase.Log("[System] 已由于更新强制结束程序");
            }
        }
        catch (Win32Exception ex)
        {
            ModBase.Log(ex, "自动更新时触发 Win32 错误，疑似被拦截", ModBase.LogLevel.Debug, "出现错误");
            if (ModMain.MyMsgBox(string.Format("由于被 Windows 安全中心拦截，或者存在权限问题，导致 PCL 无法更新。{0}请将 PCL 所在文件夹加入白名单，或者手动用 {1}PCL\\Plain Craft Launcher Community Edition.exe 替换当前文件！", Environment.NewLine, ModBase.ExePath), "更新失败", "查看帮助", "确定", "", true, true, false, null, null, null) == 1)
            {
                CustomEvent.Raise(CustomEvent.EventType.打开帮助, "启动器/Microsoft Defender 添加排除项.json");
            }
        }
    }

    /// <summary>
    ///     确保 PathTemp 下的 Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ///     如果不是，则下载一个。
    /// </summary>
    internal static void DownloadLatestPCL(ModLoader.LoaderBase LoaderToSyncProgress = null)
    {
        // 注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
        var LatestPCLPath = Path.Combine(ModBase.PathTemp, "CE-Latest.exe");
        var target = RemoteServer.GetLatestVersion(UpdateChannel.stable,
            ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64);
        if (target is null)
            throw new Exception("无法获取更新");
        if (File.Exists(LatestPCLPath) && (ModBase.GetFileSHA256(LatestPCLPath) ?? "") == (target.SHA256 ?? ""))
        {
            ModBase.Log("[System] 最新版 PCL 已存在，跳过下载");
            return;
        }

        if ((ModBase.GetFileSHA256(ModBase.ExePathWithName) ?? "") == (target.SHA256 ?? "")) // 正在使用的版本符合要求，直接拿来用
        {
            ModBase.CopyFile(ModBase.ExePathWithName, LatestPCLPath);
            return;
        }

        var loaders = RemoteServer.GetDownloadLoader(UpdateChannel.stable,
            ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, LatestPCLPath);
        var loader = new ModLoader.LoaderCombo<int>("下载最新稳定版", loaders);
        loader.Start();
        loader.WaitForExit();
    }
    
    public static ModLoader.LoaderTask<int, int> ServerLoader = new("PCL CE 服务", _ => LoadOnlineInfo(),
        Priority: ThreadPriority.BelowNormal);

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
        ModMain.MyMsgBox(@"你正在使用来自 PCL-Community 的 PCL 社区版本，遇到问题请不要向官方仓库反馈！
PCL-Community 及其成员与龙腾猫跃无从属关系，且均不会为您的使用做担保。

如果你是意外下载的社区版，建议下载官方版 PCL 使用。
如果你是意外下载的社区版，建议下载官方版 PCL 使用。
如果你是意外下载的社区版，建议下载官方版 PCL 使用。

该版本与官方版本的特性区别：
- 主题切换：仅部分固定蓝色系主题，没有计划新增其它主题。
- 百宝箱：缺失部分官方版中的内容（回声洞、千万别点）。

此提示会在启动器更新后展示一次。", "社区版本说明", "我知道了");
    }
}