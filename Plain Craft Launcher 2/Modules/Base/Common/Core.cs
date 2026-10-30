using System.IO;
using PCL.Core.App;
using PCL.Core.Utils;

namespace PCL;

public static partial class Common
{
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
    public static nint frmHandle;

    /// <summary>
    ///     程序可执行文件所在目录，以“\”结尾。
    /// </summary>
    public static readonly string ExePath = Basics.ExecutableDirectory.EndsWith(@"\")
        ? Basics.ExecutableDirectory
        : Basics.ExecutableDirectory + @"\";

    /// <summary>
    ///     程序内嵌图片文件夹路径，以“/”结尾。
    /// </summary>
    public const string PathImage =
        "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

    /// <summary>
    ///     当前程序的语言。
    /// </summary>
    public static string currentLang = "zh_CN";

    /// <summary>
    ///     设置对象。
    /// </summary>
    public static Setup setup = new();

    /// <summary>
    ///     程序的打开计时。
    /// </summary>
    public static long applicationStartTick = TimeUtils.GetTimeTick();

    /// <summary>
    ///     程序打开时的时间。
    /// </summary>
    public static DateTime applicationOpenTime = DateTime.Now;

    /// <summary>
    ///     程序是否已结束。
    /// </summary>
    public static bool isProgramEnded = false;

    /// <summary>
    ///     程序的缓存文件夹路径，以 \ 结尾。
    /// </summary>
    public static string pathTemp = Paths.Temp + @"\";

    /// <summary>
    ///     AppData 中的 PCL 文件夹路径，以 \ 结尾。
    /// </summary>
    public static string pathAppdata =
        $@"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL")}\";

    /// <summary>
    ///     AppData 中的 PCLCE 配置文件夹路径，以 \ 结尾。
    /// </summary>
    public static string pathAppdataConfig =
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        + (VersionBranchName == "Debug" ? @"\.pclcedebug\" : @"\.pclce\");

    /// <summary>
    ///     纯 ASCII 路径，用于避免部分程序对非 ASCII 路径的兼容问题。
    /// </summary>
    public static string pathPure = GetPureAsciiDir();
}