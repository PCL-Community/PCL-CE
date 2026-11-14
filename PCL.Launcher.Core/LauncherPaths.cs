using PCL.Core.App;

namespace PCL;

/// <summary>业务层使用的路径常量。</summary>
public static class LauncherPaths
{
    /// <summary>Mesa Loader Windows 版本。</summary>
    public const string MesaLoaderWindowsVersion = "26.0.4";

    /// <summary>程序内嵌图片根路径，具体 URI 方案由 UI 层注册的资源访问实现决定。</summary>
    public static string ImageRoot => Basics.GetAppImageRootUri();

    /// <summary>程序可执行文件所在目录，以路径分隔符结尾。</summary>
    public static string ExecutableDirectory { get; } =
        Basics.ExecutableDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? Basics.ExecutableDirectory
            : Basics.ExecutableDirectory + Path.DirectorySeparatorChar;

    /// <summary>临时目录，以路径分隔符结尾。</summary>
    public static string Temp => Paths.Temp + Path.DirectorySeparatorChar;

    /// <summary>可用于临时存放文件的、不含特殊字符的目录，以路径分隔符结尾。</summary>
    public static string PureAsciiDirectory
    {
        get
        {
            if (ExecutableDirectory.All(c => c < 128))
                return Path.Combine(ExecutableDirectory, "PCL") + Path.DirectorySeparatorChar;

            var temp = Path.GetTempPath();
            if (temp.All(c => c < 128))
                return temp.EndsWith(Path.DirectorySeparatorChar)
                    ? temp
                    : temp + Path.DirectorySeparatorChar;

            return !PCL.Core.Platform.PlatformRuntime.Current.Capabilities.UsesWindowsPaths
                ? Path.Combine(temp, "PCL") + Path.DirectorySeparatorChar
                : Path.Combine(
                    Path.GetPathRoot(Environment.SystemDirectory)
                    ?? Path.DirectorySeparatorChar.ToString(),
                    "ProgramData",
                    "PCL");
        }
    }

    /// <summary>当前 Minecraft 文件夹路径，由 WPF 宿主同步设置。</summary>
    public static string? MinecraftFolder { get; set; }
}