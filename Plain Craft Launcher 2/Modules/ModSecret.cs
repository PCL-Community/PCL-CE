using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI.Theme;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

internal static class ModSecret
{
    #region 杂项

#if DEBUG
    public const string RegFolder = "PCLCEDebug"; // 社区开发版的注册表与社区常规版的注册表隔离，以防数据冲突
#else
        public const string RegFolder = "PCLCE"; // PCL 社区版的注册表与 PCL 的注册表隔离，以防数据冲突
#endif

    // 用于微软登录的 ClientId
    public static readonly string OAuthClientId =
        EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    // CurseForge API Key
    public static readonly string CurseForgeAPIKey =
        EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    // 遥测鉴权密钥
    public static readonly string TelemetryKey =
        EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    // Natayark ID Client Id
    public static readonly string NatayarkClientId =
        EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    // Natayark ID Client Secret，需要经过 PASSWORD HASH 处理（https://uutool.cn/php-password/）
    public static readonly string NatayarkClientSecret =
        EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    // 联机服务根地址
    public static readonly string[] LinkServers = EnvironmentInterop
        .GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly: true).ReplaceNullOrEmpty().Split("|");

    internal static void SecretOnApplicationStart()
    {
        // 提升 UI 线程优先级
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        // 确保 .NET Framework 版本
        try
        {
            var VersionTest = new FormattedText("", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Fonts.SystemTypefaces.First(), 96d, new ModBase.MyColor(), ModBase.DPI);
        }
        catch (UriFormatException ex) // 修复 #3555
        {
            Environment.SetEnvironmentVariable("windir", Environment.GetEnvironmentVariable("SystemRoot"),
                EnvironmentVariableTarget.User);
            var VersionTest = new FormattedText("", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                Fonts.SystemTypefaces.First(), 96d, new ModBase.MyColor(), ModBase.DPI);
        }

        // 检测当前文件夹权限
        var dataPath = Paths.Data;
        try
        {
            Directory.CreateDirectory(dataPath);
        }
        catch (Exception ex)
        {
            Interaction.MsgBox(
                $$"""
                  PCL 无法创建 PCL 文件夹（{{dataPath}}），请尝试：
                  1. 将 PCL 移动到其他文件夹{{(ModBase.ExePath.StartsWithF("C:", true) ? "，例如 C 盘和桌面以外的其他位置。" : "。")}}
                  2. 删除当前目录中的 PCL 文件夹，然后再试。
                  3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。
                  """,
                MsgBoxStyle.Critical, "运行环境错误");
            Environment.Exit((int)ModBase.ProcessReturnValues.Cancel);
        }

        if (!ModBase.CheckPermission(ModBase.ExePath + "PCL"))
        {
            Interaction.MsgBox(
                $$"""
                  PCL 没有对当前文件夹的写入权限，请尝试：
                  1. 将 PCL 移动 to 其他文件夹{{(ModBase.ExePath.StartsWithF("C:", true) ? "，例如 C 盘和桌面以外的其他位置。" : "。")}}
                  2. 删除当前目录中的 PCL 文件夹，然后再试。
                  3. 右键 PCL 选择属性，打开 兼容性 中的 以管理员身份运行此程序。
                  """,
                MsgBoxStyle.Critical, "运行环境错误");
            Environment.Exit((int)ModBase.ProcessReturnValues.Cancel);
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

    /// <summary>
    ///     获取设备的短标识码
    /// </summary>
    internal static string SecretGetUniqueAddress()
    {
        return Identify.LauncherId;
    }

    internal static void SecretLaunchJvmArgs(ref List<string> DataList)
    {
        var DataJvmCustom =
            Conversions.ToString(ModBase.Setup.Get("VersionAdvanceJvm", ModMinecraft.McInstanceSelected));
        DataList.Insert(0,
            Conversions.ToString(string.IsNullOrEmpty(DataJvmCustom)
                ? Config.Launch.JvmArgs
                : DataJvmCustom)); // 可变 JVM 参数
        switch (Config.Launch.PreferredIpStack)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
            {
                DataList.Add("-Djava.net.preferIPv4Stack=true");
                DataList.Add("-Djava.net.preferIPv4Addresses=true");
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false):
            {
                DataList.Add("-Djava.net.preferIPv6Stack=true");
                DataList.Add("-Djava.net.preferIPv6Addresses=true");
                break;
            }
        }

        double availableGb = KernelInterop.GetAvailablePhysicalMemoryBytes() / 1073741824.0;
        ModLaunch.McLaunchLog($"当前剩余内存：{availableGb:N1}G");
        double totalRamMb = PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected) * 1024d;
        DataList.Add($"-Xmn{Math.Floor(totalRamMb * 0.15)}m");
        DataList.Add($"-Xmx{Math.Floor(totalRamMb)}m");
        if (!DataList.Any(d => d.Contains("-Dlog4j2.formatMsgNoLookups=true")))
            DataList.Add("-Dlog4j2.formatMsgNoLookups=true");
    }

    #endregion

    #region 网络鉴权

    internal static string SecretCdnSign(string UrlWithMark)
    {
        if (!UrlWithMark.EndsWithF("{CDN}"))
            return UrlWithMark;
        return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20");
    }

    /// <summary>
    ///     设置 Headers 的 UA、Referer。
    /// </summary>
    internal static void SecretHeadersSign(string Url, ref HttpRequestMessage Client, bool UseBrowserUserAgent = false,
        string CustomUserAgent = "")
    {
        Client.Version = HttpVersion.Version20;
        Client.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        if (Url.Contains("api.curseforge.com"))
            Client.Headers.Add("x-api-key", CurseForgeAPIKey);
        var userAgent = !string.IsNullOrEmpty(CustomUserAgent)
            ? CustomUserAgent
            : UseBrowserUserAgent
                ? $"PCL2/{ModBase.UpstreamVersion}.{ModBase.VersionBranchCode} PCLCE/{ModBase.VersionStandardCode} Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko) Chrome/136.0.0.0 Safari/537.36 Edg/136.0.0.0"
                : $"PCL2/{ModBase.UpstreamVersion}.{ModBase.VersionBranchCode} PCLCE/{ModBase.VersionStandardCode}";
        Client.Headers.Add("User-Agent", userAgent);
    }

    #endregion



    #region 更新

    public static bool IsCheckingUpdates = false;
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

    public enum VersionStatus
    {
        Latest,
        NotLatest,
        Unknown
    }

    public static VersionStatus GetVersionStatus()
    {
        try
        {
            if (IsCurrentVersionBeta && !((int)Config.Update.UpdateChannel == 1))
            {
                var isNewerThanStable = RemoteServer.IsLatest(UpdateChannel.stable,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.VersionBaseName),
                    ModBase.VersionCode);
                var isBetaLatest = RemoteServer.IsLatest(UpdateChannel.beta,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.VersionBaseName),
                    ModBase.VersionCode);
                return (VersionStatus)Conversions.ToInteger(isNewerThanStable && isBetaLatest);
            }

            return RemoteServer.IsLatest(
                Conversions.ToBoolean(IsCurrentVersionBeta) ? UpdateChannel.beta : UpdateChannel.stable,
                ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, SemVer.Parse(ModBase.VersionBaseName),
                ModBase.VersionCode)
                ? VersionStatus.Latest
                : VersionStatus.NotLatest;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "无法获取最新版本信息，请检查网络连接", ModBase.LogLevel.Hint);
            return VersionStatus.Unknown;
        }
    }

    public enum UpdateType
    {
        Silent = 0,
        PromptOnly = 1,
        DownloadAndPrompt = 2,
        UpdateNow = 3
    }

    public static ModLoader.LoaderCombo<JObject> UpdateLoader;

    public static void UpdateStart(UpdateType type, string receivedKey = null, bool forceValidated = false)
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
                if (type == UpdateType.PromptOnly)
                {
                    ModBase.Log("[Test]");
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
                    Conversions.ToBoolean(IsCurrentVersionBeta) ? UpdateChannel.beta : UpdateChannel.stable,
                    ModBase.IsArm64System ? UpdateArch.arm64 : UpdateArch.x64, dlTargetPath));
                loaders.Add(new ModLoader.LoaderTask<int, int>("校验更新", _ =>
                {
                    var curHash = ModBase.GetFileSHA256(dlTargetPath);
                    if ((curHash ?? "") != (version.SHA256 ?? ""))
                        throw new Exception($"更新文件 SHA256 不正确，应该为 {version.SHA256}，实际为 {curHash}");
                }));
                if (type == UpdateType.UpdateNow)
                    loaders.Add(new ModLoader.LoaderTask<int, int>("安装更新", _ => UpdateRestart(true)));
                else if (type == UpdateType.Silent)
                    loaders.Add(new ModLoader.LoaderTask<int, int>("准备更新", _ => IsUpdateWaitingRestart = true));
                else if (type == UpdateType.DownloadAndPrompt)
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
                if (type == UpdateType.UpdateNow)
                {
                    ModLoader.LoaderTaskbarAdd(UpdateLoader);
                    ModMain.FrmMain.BtnExtraDownload.ShowRefresh();
                    ModMain.FrmMain.BtnExtraDownload.Ribble();
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Update] 获取启动器更新失败");
                if (type != UpdateType.Silent)
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
        var LatestPCLPath = ModBase.PathTemp + "CE-Latest.exe";
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

    #endregion

    #region 联网通知

    public static ModLoader.LoaderTask<int, int> ServerLoader = new("PCL CE 服务", _ => LoadOnlineInfo(),
        Priority: ThreadPriority.BelowNormal);

    private static void LoadOnlineInfo()
    {
        var updateDesire = Config.Update.UpdateMode;
        var AnnouncementDesire = States.System.AnnounceSolution;
        switch (updateDesire)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 静默更新
            {
                ModBase.Log("[Update] 更新设置: 自动下载并安装更新");
                if (GetVersionStatus() != VersionStatus.Latest) UpdateStart(UpdateType.Silent);

                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 自动下载，提示更新
            {
                ModBase.Log("[Update] 更新设置: 自动下载并提示更新");
                UpdateStart(UpdateType.DownloadAndPrompt);
                break;
            }
            case var case2 when Operators.ConditionalCompareObjectEqual(case2, 2, false): // 提示更新
            {
                ModBase.Log("[Update] 更新设置: 提示更新");
                UpdateStart(UpdateType.PromptOnly);
                break;
            }

            default:
            {
                ModBase.Log("[Update] 更新设置: 不自动检查更新");
                return;
            }
        }

        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectLessEqual(AnnouncementDesire, 1, false)))
        {
            var ShowedAnnounced = States.Hint.ShowedAnnouncements.ToString()
                .Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).ToList();
            var ShowAnnounce = RemoteServer.GetAnnouncementList().content.Where(x => !ShowedAnnounced.Contains(x.id))
                .ToList();
            ModBase.Log("[System] 需要展示的公告数量：" + ShowAnnounce.Count);
            ModBase.RunInNewThread(() =>
            {
                foreach (var item in ShowAnnounce)
                {
                    var SelectedBtn = ModMain.MyMsgBox(item.detail, item.title, item.btn1 is null ? "" : item.btn1.text,
                        item.btn2 is null ? "" : item.btn2.text, "关闭",
                        Button1Action: () => 
                        {
                            if (Enum.TryParse<CustomEvent.EventType>(item.btn1.command, true, out var eventType))
                                CustomEvent.Raise(eventType, item.btn1.command_paramter);
                        },
                        Button2Action: () => 
                        {
                            if (Enum.TryParse<CustomEvent.EventType>(item.btn2.command, true, out var eventType))
                                CustomEvent.Raise(eventType, item.btn2.command_paramter);
                        });
                }
            });
            ShowedAnnounced.AddRange(ShowAnnounce.Select(x => x.id).ToList());
            ShowedAnnounced = ShowedAnnounced.Distinct().ToList();
            States.Hint.ShowedAnnouncements = ShowedAnnounced.Join("|");
        }
    }

    #endregion

    #region 系统信息

    internal static string CPUName;

    /// <summary>
    ///     系统 GPU 信息
    /// </summary>
    internal static List<GPUInfo> GPUs = new();

    /// <summary>
    ///     已安装物理内存大小，单位 MB
    /// </summary>
    internal static long SystemMemorySize = (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;

    /// <summary>
    ///     系统信息描述，例如 Microsoft Windows 11 专业工作站版 10.0.22635.0
    /// </summary>
    public static string OSInfo = RuntimeInformation.OSDescription + " " + Environment.OSVersion.Version;

    public class GPUInfo
    {
        internal string DriverVersion;

        /// <summary>
        ///     显存大小，单位 MB
        /// </summary>
        internal long Memory;

        internal string Name;
    }

    /// <summary>
    ///     获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    /// </summary>
    internal static void GetSystemInfo()
    {
        // CPU
        try
        {
            var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_Processor");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                CPUName = queryObj["Name"].ToString().Trim();
                break; // 通常只需要第一个CPU的信息
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取 CPU 信息时出错", ModBase.LogLevel.Normal);
        }

        // GPU
        try
        {
            var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_VideoController");

            foreach (ManagementObject queryObj in searcher.Get())
            {
                var gpuInfo = new GPUInfo();

                if (queryObj["Name"] is not null) gpuInfo.Name = Conversions.ToString(queryObj["Name"]);
                if (queryObj["AdapterRAM"] is not null)
                {
                    var ramMB = Conversions.ToLong(queryObj["AdapterRAM"]) / (1024 * 1024);
                    gpuInfo.Memory = ramMB;
                }

                if (queryObj["DriverVersion"] is not null)
                    gpuInfo.DriverVersion = Conversions.ToString(queryObj["DriverVersion"]);

                GPUs.Add(gpuInfo);
            }

            ModBase.Log("已获取系统环境信息");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取 GPU 信息时出错", ModBase.LogLevel.Normal);
        }
    }

    #endregion
    
}
