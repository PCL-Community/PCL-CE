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
    private static string _data;
    private static string _sharedData;
    private static string _sharedLocalData;
    private static string _temp;

    static Paths()
    {
#if DEBUG
        const string name = "PCLCE_Debug";
        const string oldName = ".PCLCEDebug";
#else
        const string name = "PCLCE";
        const string oldName = ".PCLCE";
#endif
        PackageKind = _ReadPackageKind();

        // fill paths
        _data = Path.Combine(DefaultDirectory, "PCL");
        _sharedData = GetSpecialPath(Special.ApplicationData, name);
        _sharedLocalData = GetSpecialPath(Special.LocalApplicationData, name);
        _temp = Path.Combine(Path.GetTempPath(), name);
        OldSharedData = GetSpecialPath(Special.ApplicationData, oldName);

        var dataOverride = Environment.GetEnvironmentVariable("PCL_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataOverride))
            _data = Path.GetFullPath(ExpandUserPath(dataOverride));
        else if (!string.IsNullOrWhiteSpace(PackageKind) || !_CanWriteDirectory(DefaultDirectory))
            _data = Path.Combine(
                Environment.GetFolderPath(Special.UserProfile),
                "PCLCE");

#if DEBUG
        // read environment variables
        EnvironmentInterop.ReadVariable("PCL_PATH", ref _data);
        EnvironmentInterop.ReadVariable("PCL_PATH_SHARED", ref _sharedData);
        EnvironmentInterop.ReadVariable("PCL_PATH_LOCAL", ref _sharedLocalData);
        EnvironmentInterop.ReadVariable("PCL_PATH_TEMP", ref _temp);
#endif
        // create directories
        Directory.CreateDirectory(_data);
        Directory.CreateDirectory(_sharedData);
        Directory.CreateDirectory(_sharedLocalData);
        Directory.CreateDirectory(_temp);
    }

    /// <summary>The default directory used for relative path combining.</summary>
    public static string DefaultDirectory => Basics.ExecutableDirectory;

    /// <summary>当前安装形态，例如 <c>deb</c>、<c>rpm</c>、<c>appimage</c>、<c>macos-app</c>。 便携构建为空。</summary>
    public static string? PackageKind { get; }

    /// <summary>Per-instance data directory.</summary>
    public static string Data
    {
        get => _data;
        set => _data = value;
    }

    /// <summary>Shared synchronized data directory.</summary>
    public static string SharedData
    {
        get => _sharedData;
        set => _sharedData = value;
    }

    /// <summary>
    /// Shared synchronized data directory of old versions.<br /> Keep the value just for
    /// migration, DO NOT USE IT.
    /// </summary>
    public static string OldSharedData { get; set; }

    /// <summary>
    /// Shared local data directory, used to put some large files that can be released or
    /// downloaded back anytime.
    /// </summary>
    public static string SharedLocalData
    {
        get => _sharedLocalData;
        set => _sharedLocalData = value;
    }

    /// <summary>
    /// Temporary files directory (can be deleted anytime, except when the program is
    /// running).
    /// </summary>
    public static string Temp
    {
        get => _temp;
        set => _temp = value;
    }

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
        if (string.IsNullOrWhiteSpace(path)) return path;
        path = path.Trim();
        if (path == "~") return Environment.GetFolderPath(Special.UserProfile);
        if (path.StartsWith("~/", StringComparison.Ordinal)
            || path.StartsWith("~\\", StringComparison.Ordinal))
            return Path.Combine(
                Environment.GetFolderPath(Special.UserProfile),
                path[2..]);
        return path;
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