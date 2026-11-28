using System;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.App;

// ReSharper disable InconsistentNaming
public static class Secrets
{
    // 官方发布包使用的 OAuth 客户端 ID。Client ID 是公开的应用标识，不是密钥；
    // 本地构建默认没有 CI 注入的密钥，缺少它会导致微软正版登录直接失败。
    private const string OfficialMsOAuthClientId = "c14b0370-8d75-42f8-b329-5b60d39e319f";

    /// <summary>
    /// 微软 OAuth 的 Client ID
    /// </summary>
    public static string MSOAuthClientId { get; } = ResolveMsOAuthClientId();

    private static string ResolveMsOAuthClientId()
    {
        // 运行时变量优先：即使发布版本嵌入了 MS_CLIENT_ID，也要允许用户切换到自己的 Azure 应用。
        var clientId = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID");
        if (string.IsNullOrWhiteSpace(clientId))
            clientId = EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: true);
        return string.IsNullOrWhiteSpace(clientId) ? OfficialMsOAuthClientId : clientId;
    }

    /// <summary>
    /// CurseForge API 的 Client ID
    /// </summary>
    public static string CurseForgeAPIKey { get; } = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 遥测密钥
    /// </summary>
    public static string TelemetryKey { get; } = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientId { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientSecret { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 联机根服务器
    /// </summary>
    public static string[] LinkServers { get; } = EnvironmentInterop.GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly: true).ReplaceNullOrEmpty().Split("|");

    /// <summary>
    /// 当前版本的 Git 提交 SHA
    /// </summary>
    public static string CommitHash { get; } = EnvironmentInterop.GetSecret("GITHUB_SHA", readEnvDebugOnly: true).ReplaceNullOrEmpty();
}
