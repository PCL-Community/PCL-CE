using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using Microsoft.VisualBasic;
using PCL.Core.App;
using PCL.Core.IO;

namespace PCL;

/// <summary>
/// Owns launcher path constants, path discovery, and path normalization helpers.
/// </summary>
public static class LauncherPaths
{
    /// <summary>程序可执行文件所在目录，以“\”结尾。</summary>
    public static readonly string ExecutableDirectory = Basics.ExecutableDirectory.EndsWith(@"\")
        ? Basics.ExecutableDirectory
        : Basics.ExecutableDirectory + @"\";

    /// <summary>程序可执行文件完整路径。</summary>
    public static readonly string ExecutablePath = Basics.ExecutablePath;

    /// <summary>系统盘盘符，以 \ 结尾。例如 "C:\"。</summary>
    public static string SystemDrive = Environment.GetLogicalDrives().Where(Directory.Exists).First().ToUpper().First() + @":\";

    /// <summary>程序的缓存文件夹路径，以 \ 结尾。</summary>
    public static string TempDirectory = Paths.Temp + @"\";

    /// <summary>AppData 中的 PCL 文件夹路径，以 \ 结尾。</summary>
    public static string AppDataDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\PCL\";

    /// <summary>AppData 中的 PCLCE 配置文件夹路径，以 \ 结尾。</summary>
    public static string AppDataConfigDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) +
                                                   (LauncherEnvironment.VersionBranchName == "Debug" ? @"\.pclcedebug\" : @"\.pclce\");

    public static string HelpDirectory = TempDirectory + @"CE\Help\";

    /// <summary>可用于临时存放文件的，不含任何特殊字符的文件夹路径，以“\”结尾。</summary>
    public static string PureAsciiDirectory = GetPureAsciiDirectory();

    public static string GetDirectoryFromPath(string filePath)
    {
        if (!(filePath.Contains(@"\") || filePath.Contains("/")))
            throw new Exception("不包含路径：" + filePath);
        if (filePath.EndsWithF(@"\") || filePath.EndsWithF("/"))
        {
            var isBackslash = filePath.EndsWithF(@"\");
            filePath = Strings.Left(filePath, Strings.Len(filePath) - 1);
            return Strings.Left(filePath, filePath.LastIndexOfAny(new[] { '\\', '/' })) + (isBackslash ? @"\" : "/");
        }

        var result = Strings.Left(filePath, filePath.LastIndexOfAny(new[] { '\\', '/' }) + 1);
        if (string.IsNullOrEmpty(result))
            throw new Exception("不包含路径：" + filePath);
        return result;
    }

    public static string GetFileName(string filePath)
    {
        filePath = filePath.Replace("/", @"\");
        if (filePath.EndsWithF(@"\"))
            throw new Exception("不包含文件名：" + filePath);
        if (filePath.Contains("?"))
            filePath = filePath.Substring(0, filePath.IndexOfF("?"));
        if (filePath.Contains(@"\"))
            filePath = filePath.Substring(filePath.LastIndexOfF(@"\") + 1);
        var length = filePath.Length;
        if (length == 0)
            throw new Exception("不包含文件名：" + filePath);
        if (length > 250)
            throw new PathTooLongException("文件名过长：" + filePath);
        return filePath;
    }

    public static string GetFileNameWithoutExtension(string filePath)
    {
        return Path.GetFileNameWithoutExtension(filePath);
    }

    public static string GetFolderName(string folderPath)
    {
        if (folderPath.EndsWithF(@":\") || folderPath.EndsWithF(@":\\"))
            return folderPath.Substring(0, 1);
        if (folderPath.EndsWithF(@"\") || folderPath.EndsWithF("/"))
            folderPath = Strings.Left(folderPath, folderPath.Length - 1);
        return GetFileName(folderPath);
    }

    public static string ShortenPath(string longPath, int shortenThreshold = 247)
    {
        if (longPath.Length <= shortenThreshold)
            return longPath;
        var shortPath = new StringBuilder(260);
        GetShortPathName(longPath, shortPath, 260);
        return shortPath.ToString();
    }

    public static Stream GetResourceStream(string path)
    {
        var resourceInfo = Application.GetResourceStream(new Uri($"pack://application:,,,/{path}", UriKind.Absolute));
        return resourceInfo?.Stream;
    }

    private static string GetPureAsciiDirectory()
    {
        if (IsAscii(ExecutableDirectory)) return ExecutableDirectory + @"PCL\";
        if (IsAscii(AppDataDirectory)) return AppDataDirectory;
        if (IsAscii(TempDirectory)) return TempDirectory;
        return SystemDrive + @"ProgramData\PCL\";
    }

    private static bool IsAscii(string input)
    {
        return input.All(c => Strings.AscW(c) < 128);
    }

    [DllImport("kernel32", EntryPoint = "GetShortPathNameA")]
    private static extern int GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, int cchBuffer);
}
