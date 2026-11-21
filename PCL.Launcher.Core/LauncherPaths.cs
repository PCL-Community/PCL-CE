using PCL.Core.App;
using PCL.Core.Platform;

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

    /// <summary>启动器用户数据根目录，以路径分隔符结尾。</summary>
    public static string UserDataRoot { get; } = _GetUserDataRoot();

    /// <summary>默认 Minecraft 文件夹，不是以路径分隔符结尾的路径。</summary>
    public static string DefaultMinecraftDirectory { get; } = _GetDefaultMinecraftDirectory();

    /// <summary>临时目录，以路径分隔符结尾。</summary>
    public static string Temp => Paths.Temp + Path.DirectorySeparatorChar;

    /// <summary>可用于临时存放文件的、不含特殊字符的目录，以路径分隔符结尾。</summary>
    public static string PureAsciiDirectory
    {
        get
        {
            var dataDirectory = Paths.Data;
            if (dataDirectory.All(c => c < 128))
                return dataDirectory.EndsWith(Path.DirectorySeparatorChar)
                    ? dataDirectory
                    : dataDirectory + Path.DirectorySeparatorChar;

            var temp = Path.GetTempPath();
            if (temp.All(c => c < 128))
                return temp.EndsWith(Path.DirectorySeparatorChar)
                    ? temp
                    : temp + Path.DirectorySeparatorChar;

            return ApplicationRuntime
                    .GetService<IPlatformPathService>()
                    ?.GetPureAsciiDirectory()
                ?? Path.Combine(temp, "PCL") + Path.DirectorySeparatorChar;
        }
    }

    /// <summary>
    /// 官方启动器的默认 Minecraft 文件夹，以路径分隔符结尾。 Windows 为 <c>%APPDATA%\.minecraft</c>，macOS 为
    /// <c>~/Library/Application Support/minecraft</c>， Linux 为 <c>~/.minecraft</c>。
    /// </summary>
    public static string OfficialMinecraftDirectory =>
        _WithTrailingSeparator(
            ApplicationRuntime
                .GetService<IPlatformPathService>()
                ?.GetOfficialMinecraftDirectory()
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".minecraft"));

    /// <summary>当前 Minecraft 文件夹路径，由 WPF 宿主同步设置。</summary>
    public static string? MinecraftFolder { get; set; }

    private static string _GetUserDataRoot()
    {
        var dataDirectory = Environment.GetEnvironmentVariable("PCL_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDirectory))
            return _WithTrailingSeparator(Paths.ExpandUserPath(dataDirectory));

        if (!string.IsNullOrWhiteSpace(Paths.PackageKind))
            return _WithTrailingSeparator(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "PCLCE"));

        return ExecutableDirectory;
    }

    private static string _GetDefaultMinecraftDirectory()
    {
        var minecraftDirectory = Environment.GetEnvironmentVariable("PCL_MINECRAFT_DIR");
        if (!string.IsNullOrWhiteSpace(minecraftDirectory))
            return Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(Paths.ExpandUserPath(minecraftDirectory)));

        var dataDirectory = Environment.GetEnvironmentVariable("PCL_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDirectory))
            return Path.TrimEndingDirectorySeparator(
                Path.Combine(
                    Path.GetFullPath(Paths.ExpandUserPath(dataDirectory)),
                    ".minecraft"));

        if (!string.IsNullOrWhiteSpace(Paths.PackageKind))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "PCLCE",
                ".minecraft");

        return Path.TrimEndingDirectorySeparator(
            Path.Combine(ExecutableDirectory, ".minecraft"));
    }

    private static string _WithTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}