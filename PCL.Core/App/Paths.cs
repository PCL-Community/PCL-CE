using System;
using System.IO;
using PCL.Core.IO;
using PCL.Core.Utils.OS;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

/// <summary>
/// Application global paths.<br /> <b>NOTE</b>: The behaviors of all path strings
/// depends on <see cref="Path" /> API provided by .NET standard library. You should use
/// <see cref="Path" /> and other APIs relative to it to process any path string from this
/// service, rather than concat paths manually.
/// </summary>
public static class Paths
{
    static Paths()
    {
#if DEBUG
        const string name = "PCLCE_Debug";
        const string oldName = ".PCLCEDebug";
        const string linuxName = "pcl-ce-debug";
#else
        const string name = "PCLCE";
        const string oldName = ".PCLCE";
        const string linuxName = "pcl-ce";
#endif
        PackageKind = _ReadPackageKind();

        var isLinux = XdgPaths.IsLinux;
        var appName = isLinux ? linuxName : name;

        // fill paths
        Data = Path.Combine(DefaultDirectory, "PCL");
        SharedData = isLinux
            ? Path.Combine(XdgPaths.ConfigHome, appName)
            : GetSpecialPath(Special.ApplicationData, name);
        SharedLocalData = isLinux
            ? Path.Combine(XdgPaths.CacheHome, appName)
            : GetSpecialPath(Special.LocalApplicationData, name);
        State = isLinux
            ? Path.Combine(XdgPaths.StateHome, appName)
            : SharedLocalData;
        Temp = Path.Combine(Path.GetTempPath(), appName);
        OldSharedData = isLinux
            ? Path.Combine(XdgPaths.ConfigHome, oldName)
            : GetSpecialPath(Special.ApplicationData, oldName);

        var dataOverride = Environment.GetEnvironmentVariable("PCL_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataOverride))
            Data = Path.GetFullPath(ExpandUserPath(dataOverride));
        else if (!string.IsNullOrWhiteSpace(PackageKind) || !_CanWriteDirectory(DefaultDirectory))
            Data = isLinux
                ? Path.Combine(XdgPaths.DataHome, appName)
                : Path.Combine(
                    Environment.GetFolderPath(Special.UserProfile),
                    "PCLCE");

#if DEBUG
        // read environment variables
        var data = Data;
        if (EnvironmentInterop.ReadVariable("PCL_PATH", ref data)) Data = data;
        var sharedData = SharedData;
        if (EnvironmentInterop.ReadVariable("PCL_PATH_SHARED", ref sharedData)) SharedData =
 sharedData;
        var sharedLocalData = SharedLocalData;
        if (EnvironmentInterop.ReadVariable("PCL_PATH_LOCAL", ref sharedLocalData))
            SharedLocalData = sharedLocalData;
        var temp = Temp;
        if (EnvironmentInterop.ReadVariable("PCL_PATH_TEMP", ref temp)) Temp = temp;
#endif
        // create directories
        Directory.CreateDirectory(Data);
        Directory.CreateDirectory(SharedData);
        Directory.CreateDirectory(SharedLocalData);
        Directory.CreateDirectory(State);
        Directory.CreateDirectory(Temp);
    }

    /// <summary>The default directory used for relative path combining.</summary>
    public static string DefaultDirectory => Basics.ExecutableDirectory;

    /// <summary>当前安装形态，例如 <c>deb</c>、<c>rpm</c>、<c>appimage</c>、<c>macos-app</c>。 便携构建为空。</summary>
    public static string? PackageKind { get; }

    /// <summary>Per-instance data directory.</summary>
    public static string Data { get; set; }

    /// <summary>Shared synchronized data directory.</summary>
    public static string SharedData { get; set; }

    /// <summary>
    /// Shared synchronized data directory of old versions.<br /> Keep the value just for
    /// migration, DO NOT USE IT.
    /// </summary>
    public static string OldSharedData { get; set; }

    /// <summary>
    /// Shared local data directory, used to put some large files that can be released or
    /// downloaded back anytime.
    /// </summary>
    public static string SharedLocalData { get; set; }

    /// <summary>
    /// State data directory. It must persist while the program is running and must not be
    /// treated as disposable cache.
    /// </summary>
    public static string State { get; set; }

    /// <summary>
    /// Temporary files directory (can be deleted anytime, except when the program is
    /// running).
    /// </summary>
    public static string Temp { get; set; }

    /// <summary>Get path string relative to a special folder.</summary>
    /// <param name="folder">the special folder</param>
    /// <param name="relative">the relative path</param>
    /// <returns>the path string relative to the special folder</returns>
    public static string GetSpecialPath(Special folder, string relative)
    {
        var folderPath = Environment.GetFolderPath(folder);
        return Path.Combine(folderPath, relative);
    }

    /// <summary>展开以 <c>~</c> 开头的用户目录路径；其他路径原样返回。</summary>
    public static string ExpandUserPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;
        path = path.Trim();
        return path switch
        {
            "~" => Environment.GetFolderPath(Special.UserProfile),
            _ when path.StartsWith("~/", StringComparison.Ordinal)
                || path.StartsWith("~\\", StringComparison.Ordinal)
                => Path.Combine(
                    Environment.GetFolderPath(Special.UserProfile),
                    path[2..]),
            _ => path
        };
    }

    private static bool _CanWriteDirectory(string directory)
    {
        return Directories.CanWrite(directory);
    }

    private static string? _ReadPackageKind()
    {
        var kind = Environment.GetEnvironmentVariable("PCL_PACKAGE_KIND");
        if (!string.IsNullOrWhiteSpace(kind))
            return kind.Trim().ToLowerInvariant();

        var candidates = new[]
        {
            Path.Combine(DefaultDirectory, ".package-kind"),
            Path.Combine(DefaultDirectory, "..", "Resources", "package-kind")
        };
        try
        {
            foreach (var marker in candidates)
            {
                if (!File.Exists(marker)) continue;
                var markerKind = File.ReadAllText(marker).Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(markerKind)) return markerKind;
            }
        }
        catch
        {
            // 打包标记不可读时按便携构建处理。
        }

        return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPIMAGE"))
            ? null
            : "appimage";
    }
}