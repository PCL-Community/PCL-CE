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
}
