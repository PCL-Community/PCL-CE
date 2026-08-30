using System.IO;
using PCL.Core.App;

namespace PCL;

/// <summary>
///     路径解析。
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

    public static string ResolveLauncherFilePath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return filePath;
        return IsAbsolutePath(filePath)
            ? filePath
            : ExecutableDirectoryWithSlash + filePath;
    }

    public static string ResolveLauncherIniPath(string fileName)
    {
        return IsAbsolutePath(fileName)
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

    public static bool IsAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
        if (path.StartsWith('/') || path.StartsWith('\\')) return true;
        if (path.Length >= 3 &&
            char.IsLetter(path[0]) &&
            path[1] == ':' &&
            path[2] is '\\' or '/') return true;

        return Path.IsPathFullyQualified(path) || Path.IsPathRooted(path);
    }

    private static string ResolvePureAsciiDirectory()
    {
        if (IsAscii(ExecutableDirectoryWithSlash)) return ExecutableDirectoryWithSlash + @"PCL\";
        if (IsAscii(LegacyAppDataWithSlash)) return LegacyAppDataWithSlash;
        if (IsAscii(TempWithSlash)) return TempWithSlash;

        return EnsureTrailingSlash(Path.Combine(SystemPaths.DriveLetter, "ProgramData", "PCL"));
    }

    private static bool IsAscii(string input)
    {
        return input.All(c => c < 128);
    }
}