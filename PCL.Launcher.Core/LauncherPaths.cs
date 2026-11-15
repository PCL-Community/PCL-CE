using PCL.Core.App;

namespace PCL;

/// <summary>业务层使用的路径常量。</summary>
public static class LauncherPaths
{
    /// <summary>Mesa Loader Windows 版本。</summary>
    public const string MesaLoaderWindowsVersion = "26.0.4";

    /// <summary>程序内嵌图片根路径。</summary>
    public const string ImageRoot =
        "pack://application:,,,/Plain Craft Launcher 2;component/Images/";

    /// <summary>程序可执行文件所在目录，以“\”结尾。</summary>
    public static string ExecutableDirectory { get; } =
        Basics.ExecutableDirectory.EndsWith('\\')
            ? Basics.ExecutableDirectory
            : Basics.ExecutableDirectory + "\\";

    /// <summary>临时目录，以“\”结尾。</summary>
    public static string Temp => Paths.Temp + "\\";

    /// <summary>可用于临时存放文件的、不含特殊字符的目录，以“\”结尾。</summary>
    public static string PureAsciiDirectory
    {
        get
        {
            if (ExecutableDirectory.All(c => c < 128))
                return Path.Combine(ExecutableDirectory, "PCL") + "\\";
            var temp = Path.GetTempPath();
            if (temp.All(c => c < 128))
                return temp.EndsWith('\\') ? temp : temp + "\\";
            return Path.Combine(
                Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
                "ProgramData",
                "PCL");
        }
    }

    /// <summary>当前 Minecraft 文件夹路径，由 WPF 宿主同步设置。</summary>
    public static string? MinecraftFolder { get; set; }
}