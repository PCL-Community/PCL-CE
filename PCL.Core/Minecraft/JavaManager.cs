using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;
using PCL.Core.Logging;
using PCL.Core.Utils.Exts;
using PCL.Core.App;

namespace PCL.Core.Minecraft;

public class JavaManager
{
    internal List<JavaInfo> InternalJavas = [];
    public List<JavaInfo> JavaList => [.. InternalJavas];

    private void _SortJavaList()
    {
        InternalJavas = (from j in InternalJavas
            orderby j.Version descending, j.Brand
            select j).ToList();
    }

    private static readonly string[] _ExcludeFolderName = ["javapath", "java8path", "common files"];

    private Task? _scanTask;
    /// <summary>
    /// 扫描 Java 会对当前已有的结果进行选择性保留
    /// </summary>
    /// <returns></returns>
    public async Task ScanJavaAsync()
    {
        if (_scanTask == null || _scanTask.IsCompleted)
            _scanTask = Task.Run(async () =>
            {
                var javaPaths = new ConcurrentBag<string>();

                Task[] searchTasks = [
                    Task.Run(() => _ScanRegistryForJava(ref javaPaths)),
                    Task.Run(() => _ScanDefaultInstallPaths(ref javaPaths)),
                    Task.Run(() => _ScanPathEnvironmentVariable(ref javaPaths)),
                    Task.Run(() => _ScanMicrosoftStoreJava(ref javaPaths)),
                    Task.Run(() => _ScanFromWhereCommand(ref javaPaths))
                    ];
                await Task.WhenAll(searchTasks);

                // 记录之前设置为禁用的 Java
                var oldJavaList = InternalJavas.ToDictionary(x => x.JavaExePath);
                // 新搜索到的 Java 路径
                var newJavaList = new HashSet<string>(
                    InternalJavas
                        .Select(x => x.JavaExePath)
                        .Concat(javaPaths)
                        .Select(x => x.TrimEnd(Path.DirectorySeparatorChar)),
                    StringComparer.OrdinalIgnoreCase);

                var ret = newJavaList
                    .Where(x => !x.Split(Path.DirectorySeparatorChar).Any(part => _ExcludeFolderName.Contains(part, StringComparer.OrdinalIgnoreCase)))
                    .Select(JavaInfo.Parse).Where(x => x != null).Select(x => x!).ToList();
                foreach (var item in ret)
                {
                    if (oldJavaList.TryGetValue(item.JavaExePath, out var existing))
                        item.IsEnabled = existing.IsEnabled;
                }

                InternalJavas = ret;
                _SortJavaList();
            });
        await _scanTask;
    }

    public void Add(JavaInfo j)
    {
        ArgumentNullException.ThrowIfNull(j);
        if (HasJava(j.JavaExePath))
            return;
        InternalJavas.Add(j);
        _SortJavaList();
    }

    public void Add(string javaExe)
    {
        ArgumentNullException.ThrowIfNull(javaExe);
        if (HasJava(javaExe))
            return;
        var temp = JavaInfo.Parse(javaExe);
        if (temp == null)
            return;
        InternalJavas.Add(temp);
        _SortJavaList();
    }

    public bool HasJava(string javaExe)
    {
        ArgumentNullException.ThrowIfNull(javaExe);
        if (!File.Exists(javaExe))
            throw new ArgumentException("Not a valid java file");
        return InternalJavas.Any(x => x.JavaExePath == javaExe);
    }

    /// <summary>
    /// 依据版本要求自动选择 Java
    /// </summary>
    /// <param name="minVersion">最小版本号</param>
    /// <param name="maxVersion">最大版本号</param>
    /// <returns></returns>
    public async Task<List<JavaInfo>> SelectSuitableJava(Version minVersion, Version maxVersion)
    {
        if (InternalJavas.Count == 0)
            await ScanJavaAsync();
        
        return (from j in InternalJavas
            where j.IsStillAvailable && j.IsEnabled
                                     && IsJavaVersionSuitable(j.Version, minVersion, maxVersion)
            orderby j.Version, j.IsJre, j.Brand // 选择最小版本的 JDK 中的合适品牌的 Java
            select j).ToList();
    }
    
    /// <summary>
    /// 将 Java 版本转换为统一格式进行比较
    /// 例如：1.8.0.140 → 8.0.140，8.0.472 → 8.0.472，9.0.1 → 9.0.1
    /// </summary>
    private static Version NormalizeJavaVersion(Version version)
    {
        return version.Major == 1 ? new Version(version.Minor, version.Build, version.Revision) : version;
    }
    
    /// <summary>
    /// 检查 Java 版本是否在指定范围内
    /// </summary>
    private static bool IsJavaVersionSuitable(Version javaVersion, Version minVersion, Version maxVersion)
    {
        // 将所有版本转换为统一格式进行比较
        var normalizedJava = NormalizeJavaVersion(javaVersion);
        var normalizedMin = NormalizeJavaVersion(minVersion);
        var normalizedMax = NormalizeJavaVersion(maxVersion);
        
        return normalizedJava >= normalizedMin && normalizedJava <= normalizedMax;
    }

    /// <summary>
    /// 检查并移除已不存在的 Java
    /// </summary>
    /// <returns></returns>
    public void CheckJavaAvailability()
    {
        InternalJavas = [..from j in InternalJavas where j.IsStillAvailable select j];
    }

    private static void _ScanRegistryForJava(ref ConcurrentBag<string> javaPaths)
    {
        // JavaSoft
        var registryPaths = new List<string>
        {
            @"SOFTWARE\JavaSoft\Java Development Kit",
            @"SOFTWARE\JavaSoft\Java Runtime Environment",
            @"SOFTWARE\WOW6432Node\JavaSoft\Java Development Kit",
            @"SOFTWARE\WOW6432Node\JavaSoft\Java Runtime Environment"
        };

        foreach (var regPath in registryPaths)
        {
            using var regKey = Registry.LocalMachine.OpenSubKey(regPath);
            if (regKey is null) continue;
            foreach (var subKeyName in regKey.GetSubKeyNames())
            {
                using var subKey = regKey.OpenSubKey(subKeyName);
                var javaHome = subKey?.GetValue("JavaHome") as string;
                if (string.IsNullOrEmpty(javaHome)
                    || Path.GetInvalidPathChars().Any(x => javaHome.Contains(x)))
                    continue;
                var javaExePath = Path.Combine(javaHome, "bin", "java.exe");
                if (File.Exists(javaExePath)) javaPaths.Add(javaExePath);
            }
        }

        //Brand Java Register Path
        string[] brandKeyNames = [
            @"SOFTWARE\Azul Systems\Zulu",
            @"SOFTWARE\BellSoft\Liberica"
            ];
        foreach (var key in brandKeyNames)
        {
            var zuluKey = Registry.LocalMachine.OpenSubKey(key);
            if (zuluKey == null) continue;
            foreach (var subKeyName in zuluKey.GetSubKeyNames())
            {
                var path = zuluKey.OpenSubKey(subKeyName)?.GetValue("InstallationPath") as string;
                if (string.IsNullOrEmpty(path)
                    || Path.GetInvalidPathChars().Any(x => path.Contains(x)))
                    continue;
                var javaExePath = Path.Combine(path, "bin", "java.exe");
                if (!File.Exists(javaExePath)) continue;
                javaPaths.Add(javaExePath);
            }
        }
    }

    // 可能的目录关键词列表
    private static readonly string[] _MostPossibleKeyWords =
    [
        "java", "jdk", "jre",
        "dragonwell", "azul", "zulu", "oracle", "open", "amazon", "corretto", "eclipse" , "temurin", "hotspot", "semeru", "kona", "bellsoft"
    ];
    
    private static readonly string[] _PossibleKeyWords =
    [
        "environment", "env", "runtime", "x86_64", "amd64", "arm64",
        "pcl", "hmcl", "baka", "minecraft"
    ];

    private static readonly string[] _TotalKeyWords = [.._MostPossibleKeyWords.Concat(_PossibleKeyWords)];

    // 最大文件夹搜索深度，一般来说 8 够用了
    private const int MaxSearchDepth = 8;

    private static void _ScanDefaultInstallPaths(ref ConcurrentBag<string> javaPaths)
    {
        // 准备欲搜索目录
        var programFilesPaths = new HashSet<string>()
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.Combine(Basics.ExecutableDirectory, "PCL")
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // 特定目录搜索
            string[] keyFolders =
            [
                "Program Files",
                "Program Files (x86)"
            ];
            var suitableDrives = DriveInfo.GetDrives().Where(d => d is { IsReady: true, DriveType: DriveType.Fixed }).ToArray();
            foreach (var folder in from driver in suitableDrives
                     from keyFolder in keyFolders
                     select Path.Combine(driver.Name, keyFolder))
            {
                programFilesPaths.Add(folder);
            }
            // 根目录搜索
            foreach (var dri in from d in suitableDrives select d.Name)
            {
                if (dri.IsNullOrEmpty()) continue;
                IEnumerable<string>? subDirs = null;
                try
                {
                    subDirs = Directory.EnumerateDirectories(dri);
                }catch(UnauthorizedAccessException){/* 忽略无权限访问的根目录 */}
                catch (DirectoryNotFoundException) { /* 忽略找不到的目录 */ }
                catch (IOException) { /* 忽略IO异常 */ }

                if (subDirs is null) continue;
                foreach (var folder in from dir in subDirs
                         where _MostPossibleKeyWords.Any(x => Path.GetFileName(dir).Contains(x, StringComparison.OrdinalIgnoreCase))
                         select dir)
                {
                    programFilesPaths.Add(folder);
                }
            }
        }
        else
        {
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86Path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            
            if (!programFilesPath.IsNullOrEmpty() && Directory.Exists(programFilesPath))
                programFilesPaths.Add(programFilesPath);
                
            if (!programFilesX86Path.IsNullOrEmpty() && Directory.Exists(programFilesX86Path))
                programFilesPaths.Add(programFilesX86Path);
        }
        LogWrapper.Info($"[Java] 对下列目录进行广度关键词搜索{Environment.NewLine}{string.Join(Environment.NewLine, programFilesPaths)}");
        
        // 使用 广度优先搜索 查找 Java 文件
        foreach (var rootPath in programFilesPaths)
        {
            var queue = new Queue<(string path, int depth)>();
            queue.Enqueue((rootPath, 0));
            while (queue.Count > 0)
            {
                var (currentPath, depth) = queue.Dequeue();
                if (depth > MaxSearchDepth) continue;
                try
                {
                    if (!Directory.Exists(currentPath)) continue;
                    // 只遍历包含关键字的目录
                    var subDirs = depth == 0
                        ? Directory.EnumerateDirectories(currentPath)
                            .Where(x => _TotalKeyWords.Any(k =>
                                Path.GetFileName(x).Contains(k, StringComparison.OrdinalIgnoreCase)))
                        : Directory.EnumerateDirectories(currentPath);
                    foreach (var dir in subDirs)
                    {
                        if (!Directory.Exists(dir)) continue;
                        // 检查是否存在 java.exe
                        var javaExePath = Path.Combine(dir, "java.exe");
                        if (File.Exists(javaExePath))
                            javaPaths.Add(javaExePath);
                        else
                            queue.Enqueue((dir, depth + 1));
                    }
                }
                catch (Exception ex)
                {
                    LogWrapper.Error(ex, "Java", $"搜索 {currentPath} (depth={depth}) 过程中遇到了一个错误");
                }
            }
        }
    }

    private static void _ScanPathEnvironmentVariable(ref ConcurrentBag<string> javaPaths)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return;

        var paths = pathEnv.Split([';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var targetPath in paths)
        {
            if (!Directory.Exists(targetPath)) continue;
            var javaExePath = Path.Combine(targetPath, "java.exe");
            if (File.Exists(javaExePath))
                javaPaths.Add(javaExePath);
        }
    }

    private static void _ScanFromWhereCommand(ref ConcurrentBag<string> javaPaths)
    {
        try
        {
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "where",
                    Arguments = "java",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0) return;
            var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var javaPath = line.Trim();
                if (File.Exists(javaPath))
                {
                    javaPaths.Add(javaPath);
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Java", "通过 where 命令搜索 Java 时发生错误");
        }
    }

    private static void _ScanMicrosoftStoreJava(ref ConcurrentBag<string> javaPaths)
    {
        var storeJavaFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages",
            "Microsoft.4297127D64EC6_8wekyb3d8bbwe", // Ms Java 的固定下载地址
            "LocalCache",
            "Local",
            "runtime");
        if (!Directory.Exists(storeJavaFolder))
            return;
        // 搜索第一级目录：以"java-runtime"开头的文件夹
        foreach (var runtimeDir in Directory.EnumerateDirectories(storeJavaFolder))
        {
            var dirName = Path.GetFileName(runtimeDir);
            if (!dirName.StartsWith("java-runtime"))
                continue;

            // 搜索第二级目录：平台架构目录 (如 windows-x64)
            foreach (var archDir in Directory.EnumerateDirectories(runtimeDir))
            {
                // 搜索第三级目录：具体运行时版本目录
                foreach (var versionDir in Directory.EnumerateDirectories(archDir))
                {
                    // 检查bin/java.exe是否存在
                    var javaExePath = Path.Combine(versionDir, "bin", "java.exe");
                    if (File.Exists(javaExePath))
                    {
                        LogWrapper.Info($"[Java] 搜寻到可能的 Microsoft 官方 Java {javaExePath}");
                        javaPaths.Add(javaExePath);
                    }
                }
            }
        }
    }
}
