using PCL.Core.App;

namespace PCL;

/// <summary>
///     PCL2 侧启动器环境信息与历史全局字段的宿主。
/// </summary>
public static class LauncherEnvironment
{
    // 下列版本信息由更新器自动修改。
    public static readonly string VersionBaseName = Basics.VersionName;
    public static readonly string VersionStandardCode = Basics.Metadata.Version.StandardVersion;
    public static readonly string UpstreamVersion = Basics.Metadata.Version.UpstreamVersion;
    public static readonly string CommitHash = Basics.Metadata.Version.Commit;
    public static readonly string CommitHashShort = Basics.Metadata.Version.CommitDigest;
    public static readonly int VersionCode = Basics.VersionCode;

#if DEBUG
    public const string VersionBranchName = "Debug";
    public const string VersionBranchCode = "100";
#elif DEBUGCI
    public const string VersionBranchName = "CI";
    public const string VersionBranchCode = "50";
#else
    public const string VersionBranchName = "Publish";
    public const string VersionBranchCode = "0";
#endif

    /// <summary>
    ///     主窗口句柄。
    /// </summary>
    public static nint MainWindowHandle { get; set; }

    /// <summary>
    ///     当前程序的语言。
    /// </summary>
    public static string CurrentLanguage { get; set; } = "zh_CN";

    /// <summary>
    ///     设置对象。
    /// </summary>
    public static ModSetup Setup { get; set; } = new();
}