using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.App;
using PCL.Core.Utils;

namespace PCL;

/// <summary>
/// Owns launcher version constants, startup state, process flags, and canonical setup access.
/// </summary>
public static class LauncherEnvironment
{
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

    public static readonly string VersionBaseName = Basics.VersionName;
    public static readonly string VersionStandardCode = Basics.Metadata.Version.StandardVersion;
    public static readonly string UpstreamVersion = Basics.Metadata.Version.UpstreamVersion;
    public static readonly string CommitHash = Basics.Metadata.Version.Commit;
    public static readonly string CommitHashShort = Basics.Metadata.Version.CommitDigest;
    public static readonly int VersionCode = Basics.VersionCode;
    public static readonly string PathImage = "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

    public static readonly ModSetup Setup = new();

    public static nint FrmHandle;
    public static string Lang = "zh_CN";
    public static long ApplicationStartTick = TimeUtils.GetTimeTick();
    public static DateTime ApplicationOpenTime = DateTime.Now;
    public static string UniqueAddress = ModSecret.SecretGetUniqueAddress();
    public static bool IsProgramEnded;
    public static bool Is32BitSystem = !Environment.Is64BitOperatingSystem;
    public static bool IsArm64System = RuntimeInformation.OSArchitecture == Architecture.Arm64;
    public static bool IsGBKEncoding = Encoding.Default.CodePage == 936;
}
