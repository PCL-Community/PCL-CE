using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualBasic;
using PCL.Core.App;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL;

internal static class ModSecret
{
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
        // 确保 WPF 字体渲染环境正常
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
}
