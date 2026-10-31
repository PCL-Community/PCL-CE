using System;
using System.Diagnostics;
using System.IO;
using PCL.Core.Utils.OS;
using Special = System.Environment.SpecialFolder;

namespace PCL.Core.App;

/// <summary>
/// Application global paths.<br/>
/// <b>NOTE</b>: The behaviors of all path strings depends on <see cref="Path"/> API provided by
/// .NET standard library. You should use <see cref="Path"/> and other APIs relative to it to process
/// any path string from this service, rather than concat paths manually.
/// </summary>
public static class Paths
{
    /// <summary>
    /// The default directory used for relative path combining.
    /// </summary>
    public static string DefaultDirectory => Basics.ExecutableDirectory;

    private static string _data;
    private static string _sharedData;
    private static string _sharedLocalData;
    private static string _temp;
    
    /// <summary>
    /// Per-instance data directory.
    /// </summary>
    public static string Data { get => _data; set => _data = value; }

    /// <summary>
    /// Shared synchronized data directory.
    /// </summary>
    public static string SharedData { get => _sharedData; set => _sharedData = value; }
    
    /// <summary>
    /// Shared synchronized data directory of old versions.<br/>
    /// Keep the value just for migration, DO NOT USE IT.
    /// </summary>
    public static string OldSharedData { get; set; }

    /// <summary>
    /// Shared local data directory, used to put some large files that can be released or downloaded back anytime.
    /// </summary>
    public static string SharedLocalData { get => _sharedLocalData; set => _sharedLocalData = value; }
    
    /// <summary>
    /// Temporary files directory (can be deleted anytime, except when the program is running).
    /// </summary>
    public static string Temp { get => _temp; set => _temp = value; }

    /// <summary>
    /// Get path string relative to a special folder.
    /// </summary>
    /// <param name="folder">the special folder</param>
    /// <param name="relative">the relative path</param>
    /// <returns>the path string relative to the special folder</returns>
    public static string GetSpecialPath(Special folder, string relative)
    {
        var folderPath = Environment.GetFolderPath(folder);
        return Path.Combine(folderPath, relative);
    }

    static Paths()
    {
#if DEBUG
        const string name = "PCLCE_Debug";
        const string oldName = ".PCLCEDebug";
#else
        const string name = "PCLCE";
        const string oldName = ".PCLCE";
#endif
        // fill paths
        _data = Path.Combine(DefaultDirectory, "PCL");
        _sharedData = GetSpecialPath(Special.ApplicationData, name);
        _sharedLocalData = GetSpecialPath(Special.LocalApplicationData, name);
        _temp = Path.Combine(Path.GetTempPath(), name);
        OldSharedData = GetSpecialPath(Special.ApplicationData, oldName);
#if DEBUG
        // read environment variables
        EnvironmentInterop.ReadVariable("PCL_PATH", ref _data);
        EnvironmentInterop.ReadVariable("PCL_PATH_SHARED", ref _sharedData);
        EnvironmentInterop.ReadVariable("PCL_PATH_LOCAL", ref _sharedLocalData);
        EnvironmentInterop.ReadVariable("PCL_PATH_TEMP", ref _temp);
#endif
        // create directories
        // Paths 被全项目引用，静态构造函数一旦抛出异常，后续任何访问都只会得到笼统的
        // TypeInitializationException，既无法定位是哪个目录出了问题，也无法恢复。
        // 因此这里逐目录做“写-删探针”预检，首选位置不可写时回退到用户目录。
        _sharedLocalData = _EnsureDirectory(_sharedLocalData, null, "共享本地数据");
        _temp = _EnsureDirectory(_temp, Path.Combine(_sharedLocalData, "Temp"), "临时文件");
        _data = _EnsureDirectory(_data, Path.Combine(_sharedLocalData, "Data"), "数据");
        _sharedData = _EnsureDirectory(_sharedData, Path.Combine(_sharedLocalData, "SharedData"), "共享数据");
    }

    /// <summary>
    /// 确保目录存在且可写，首选位置不可写时回退到 <paramref name="fallback"/>。
    /// </summary>
    /// <param name="primary">首选目录</param>
    /// <param name="fallback">回退目录，为 <see langword="null"/> 表示没有回退</param>
    /// <param name="name">用于日志的目录名称</param>
    /// <returns>实际可用的目录，两者都不可用时返回首选目录</returns>
    private static string _EnsureDirectory(string primary, string? fallback, string name)
    {
        if (_TryPrepareDirectory(primary)) return primary;

        if (fallback is not null && !string.Equals(fallback, primary, StringComparison.OrdinalIgnoreCase)
                                 && _TryPrepareDirectory(fallback))
        {
            Trace.WriteLine($"[Paths] {name}目录 '{primary}' 不可写，已回退到 '{fallback}'");
            return fallback;
        }

        Trace.WriteLine($"[Paths] {name}目录 '{primary}' 不可写，且没有可用的回退目录");
        return primary;
    }

    /// <summary>
    /// 创建目录并实际写入一次以确认可写。<br/>
    /// 仅调用 <see cref="Directory.CreateDirectory(string)"/> 是不够的：目录已存在时它不会报错，
    /// 即使该目录对当前用户是只读的。
    /// </summary>
    private static bool _TryPrepareDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var probe = Path.Combine(path, $".pcl_write_probe_{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
