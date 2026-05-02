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
}
