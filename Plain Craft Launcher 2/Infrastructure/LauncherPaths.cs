using System.IO;
using PCL.Core.App;
using PCL.Core.Utils.OS;

namespace PCL;

/// <summary>
///     PCL2 历史路径与兼容路径解析。
/// </summary>
public static class LauncherPaths
{
    private const string DirectorySeparator = @"\";

    /// <summary>
    ///     程序可执行文件所在目录，以“\”结尾。
    /// </summary>
    public static string ExecutableDirectoryWithSlash { get; } =
        EnsureTrailingSlash(Basics.ExecutableDirectory);

    /// <summary>
    ///     程序内嵌图片文件夹路径，以“/”结尾。
    /// </summary>
    public static string ImageBaseUri { get; } =
        "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

    /// <summary>
    ///     程序的缓存文件夹路径，以“\”结尾。
    /// </summary>
    public static string TempWithSlash { get; set; } = EnsureTrailingSlash(Paths.Temp);

    /// <summary>
    ///     AppData 中的 PCL 文件夹路径，以“\”结尾。
    /// </summary>
    public static string LegacyAppDataWithSlash { get; set; } =
        EnsureTrailingSlash(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCL"));

    /// <summary>
    ///     AppData 中的 PCLCE 配置文件夹路径，以“\”结尾。
    /// </summary>
    public static string SharedConfigWithSlash { get; set; } =
        EnsureTrailingSlash(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                            (LauncherEnvironment.VersionBranchName == "Debug" ? @"\.pclcedebug" : @"\.pclce"));

    /// <summary>
    ///     可用于临时存放文件的，不含任何特殊字符的文件夹路径。
    /// </summary>
    public static string PureAsciiDirectory { get; set; } = ResolvePureAsciiDirectory();

    public static string ResolveLegacyFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return filePath;
        return IsWindowsAbsolutePath(filePath)
            ? filePath
            : ExecutableDirectoryWithSlash + filePath;
    }

    public static string ResolveLegacyIniPath(string fileName)
    {
        return IsWindowsAbsolutePath(fileName)
            ? fileName
            : $@"{ExecutableDirectoryWithSlash}PCL\{fileName}.ini";
    }

    public static string EnsureTrailingSlash(string path)
    {
        if (string.IsNullOrEmpty(path))
            return DirectorySeparator;
        return path.EndsWith('\\') || path.EndsWith('/')
            ? path
            : path + DirectorySeparator;
    }

    public static bool IsWindowsAbsolutePath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.Contains(@":\", StringComparison.Ordinal);
    }

    private static string ResolvePureAsciiDirectory()
    {
        if (IsAscii(ExecutableDirectoryWithSlash)) return ExecutableDirectoryWithSlash + @"PCL\";
        if (IsAscii(LegacyAppDataWithSlash)) return LegacyAppDataWithSlash;
        if (IsAscii(TempWithSlash)) return TempWithSlash;

        return Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL");
    }

    private static bool IsAscii(string input)
    {
        return input.All(c => c < 128);
    }
}