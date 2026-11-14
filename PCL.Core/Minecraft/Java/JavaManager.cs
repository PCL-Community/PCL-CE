using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Java;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.Platform;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.Java;

public class JavaManager(
    IJavaParser parser,
    params IJavaScanner[] scanners)
{
    private const string ModuleName = "JavaManager";
    private static readonly TimeSpan _MinScanInterval = TimeSpan.FromSeconds(13);
    private readonly Dictionary<string, JavaEntry> _javaEntries = new();

    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private DateTime _lastScanTime = DateTime.MinValue;

    public void SaveConfig()
    {
        try
        {
            var items = _javaEntries
                .Select(x => new JavaStorageItem
                {
                    Path = x.Value.Installation.JavaExePath,
                    IsEnable = x.Value.IsEnabled,
                    Source = x.Value.Source
                })
                .ToArray();
            States.Game.JavaList = JsonSerializer.Serialize(items, JsonCompat.SerializerOptions);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, "保存 Java 配置项失败");
        }
    }

    public void ReadConfig()
    {
        try
        {
            var items = JsonSerializer.Deserialize<JavaStorageItem[]>(
                States.Game.JavaList,
                JsonCompat.SerializerOptions);
            if (items is null) return;

            var itemsAdded = new List<JavaEntry>();

            foreach (var item in items)
            {
                var parserResult = _ParseInstallation(item.Path);
                parserResult.IfSuccess(installation => itemsAdded.Add(
                    new JavaEntry
                    {
                        Installation = installation,
                        IsEnabled = item.IsEnable,
                        Source = item.Source ?? JavaSource.AutoScanned
                    }));
                parserResult.IfFailure(_ =>
                    LogWrapper.Trace(ModuleName, $"Can not find Java {item.Path}, skip"));
            }

            lock (_javaEntries)
            {
                foreach (var item in itemsAdded)
                    if (_javaEntries.TryGetValue(
                            item.Installation.JavaExePath,
                            out var existingRecord))
                    {
                        existingRecord.IsEnabled = item.IsEnabled;
                        existingRecord.Source = item.Source;
                    }
                    else
                    {
                        _javaEntries.Add(item.Installation.JavaExePath, item);
                    }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, "无法读取 Java 配置项");
        }
    }

    /// <summary>
    ///     扫描 Java 安装
    /// </summary>
    public async Task ScanJavaAsync(bool force = false)
    {
        if (ShouldSkip()) return;

        if (!await _scanLock.WaitAsync(TimeSpan.FromSeconds(7))) return;
        try
        {
            if (ShouldSkip()) return;

            await Task.Run(_ScanInternal);
            _lastScanTime = DateTime.Now;
            SaveConfig();
        }
        finally
        {
            _scanLock.Release();
        }

        return;

        bool ShouldSkip()
        {
            return !force && DateTime.Now - _lastScanTime < _MinScanInterval;
        }
    }

    private void _ScanInternal()
    {
        var pathSet = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        Parallel.ForEach(
            scanners,
            scanner =>
            {
                var temp = new List<string>();
                scanner.Scan(temp);
                foreach (var normalized in temp
                             .Select(_NormalizePath)
                             .Where(normalized => !_ShouldExcludePath(normalized)))
                    pathSet.TryAdd(normalized, true);
            });

        Dictionary<string, JavaEntry> existingEntries;
        lock (_javaEntries)
        {
            existingEntries = new Dictionary<string, JavaEntry>(_javaEntries);
        }

        var scannedEntries = new List<JavaEntry>();
        foreach (var parserResult in pathSet.Keys.Select(_ParseInstallation))
        {
            parserResult.IfSuccess(installation => scannedEntries.Add(
                new JavaEntry
                {
                    Installation = installation,
                    IsEnabled = existingEntries.TryGetValue(
                        _NormalizePath(installation.JavaExePath),
                        out var existingJava)
                        ? existingJava.IsEnabled
                        : _ShouldEnableByDefault(installation),
                    Source = JavaSource.AutoScanned
                }));
            parserResult.IfFailure(error => LogWrapper.Warn(error, "Unable to parse Java file"));
        }

        lock (_javaEntries)
        {
            foreach (var entry in scannedEntries)
                _javaEntries[entry.Installation.JavaExePath] = entry;
        }
    }

    private Result<JavaInstallation, Exception> _ParseInstallation(string javaExePath)
    {
        return parser.Parse(javaExePath).Map(installation => installation with
        {
            JavaFolder = _ResolveDirectoryPath(installation.JavaFolder)
        });
    }

    private static bool _ShouldEnableByDefault(JavaInstallation inst)
    {
        if (!PlatformRuntime.Current.Capabilities.SupportsJavaw)
            return !(inst.Is64Bit ^ Environment.Is64BitOperatingSystem || !inst.IsStillAvailable);

        var libDir = Path.Combine(Directory.GetParent(inst.JavaFolder)!.FullName, "lib");
        var isUsable = (!inst.IsJre && File.Exists(Path.Combine(libDir, "jvm.lib")))
            || (inst.IsJre && File.Exists(Path.Combine(libDir, "rt.jar")));

        return !(inst is { IsJre: true, MajorVersion: > 8 }
            || inst.Is64Bit ^ Environment.Is64BitOperatingSystem
            || !isUsable);
    }

    public List<JavaEntry> GetSortedJavaList()
    {
        var ret = _javaEntries.Values.ToList();
        ret.Sort((a, b) =>
        {
            var versionCmp = a.Installation.Version.CompareTo(b.Installation.Version);
            if (versionCmp != 0) return versionCmp;
            return a.Installation.Brand - b.Installation.Brand;
        });
        ret.Reverse();
        return ret;
    }

    public bool Existing64BitJava()
    {
        lock (_javaEntries)
        {
            return _javaEntries.Any(x => x.Value.Installation.Is64Bit);
        }
    }

    public bool ExistAnyJava()
    {
        return _javaEntries.Count != 0;
    }

    public bool Exist(string javaExePath)
    {
        return _javaEntries.ContainsKey(javaExePath);
    }

    /// <summary>
    ///     获取，如果没有就加入记录
    /// </summary>
    /// <param name="javaExePath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public JavaEntry? AddOrGet(string javaExePath)
    {
        try
        {
            if (javaExePath.IsNullOrWhiteSpace() || !File.Exists(javaExePath)) return null;

            return _ParseInstallation(javaExePath).Match<JavaEntry?>(
                installation =>
                {
                    var exePath = _NormalizePath(installation.JavaExePath);
                    lock (_javaEntries)
                    {
                        if (_javaEntries.TryGetValue(exePath, out var ret))
                            return ret;

                        var entry = new JavaEntry
                        {
                            Installation = installation,
                            IsEnabled = _ShouldEnableByDefault(installation),
                            Source = JavaSource.ManualAdded
                        };

                        _javaEntries.Add(exePath, entry);
                        return entry;
                    }
                },
                _ => null);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, $"Failed to add or get {javaExePath}");
            return null;
        }
    }

    /// <summary>
    ///     仅获取，如果没有不增加记录
    /// </summary>
    public JavaEntry? Get(string javaExePath)
    {
        try
        {
            if (javaExePath.IsNullOrWhiteSpace() || !File.Exists(javaExePath)) return null;

            return _ParseInstallation(javaExePath).Match<JavaEntry?>(
                installation =>
                {
                    var exePath = _NormalizePath(installation.JavaExePath);
                    lock (_javaEntries)
                    {
                        if (_javaEntries.TryGetValue(exePath, out var ret))
                            return ret;

                        return new JavaEntry
                        {
                            Installation = installation,
                            IsEnabled = _ShouldEnableByDefault(installation),
                            Source = JavaSource.ManualAdded
                        };
                    }
                },
                _ => null);
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, $"Failed to get {javaExePath}");
            return null;
        }
    }

    public async Task<JavaEntry[]> SelectSuitableJavaAsync(Version minVersion, Version maxVersion)
    {
        if (_javaEntries.Count == 0)
            await ScanJavaAsync();

        lock (_javaEntries)
        {
            return
            [
                .. _javaEntries
                    .Values.ToList()
                    .Where(j =>
                        j.Installation.IsStillAvailable
                        && j.IsEnabled
                        && IsVersionSuitable(j.Installation.Version, minVersion, maxVersion))
                    .OrderBy(static j => j.Installation.MajorVersion) // 确保首要选择的大版本正确
                    .ThenBy(static j => j.Installation.IsJre) // JDK 优先
                    .ThenBy(static j => j.Installation.Brand) // Java 发行版优选
                    .ThenByDescending(static j => j.Installation.Version) // 优选后小版本号较高的版本
            ];
        }
    }

    public void CheckAllAvailability()
    {
        lock (_javaEntries)
        {
            var keys4Remove = _javaEntries
                .Where(kv => !kv.Value.Installation.IsStillAvailable)
                .Select(kv => kv.Key)
                .ToArray();
            foreach (var key in keys4Remove)
                _javaEntries.Remove(key);
        }
    }

    // ===== 路径工具 =====
    private static string _NormalizePath(string path)
    {
        return Path
            .GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>
    ///     逐级解析路径中的目录软链，使 /usr/lib/jvm/jre-25-openjdk 这类目录软链与真实目录归一到同一个 JavaFolder。
    /// </summary>
    private static string _ResolveDirectoryPath(string directory)
    {
        var fullPath = Path.GetFullPath(directory);
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var current = root;
        foreach (var part in fullPath[root.Length..]
                     .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = Path.Combine(current, part);
            current = _ResolveLinkTarget(next) ?? next;
        }

        return current;
    }

    private static string? _ResolveLinkTarget(string path)
    {
        try
        {
            return Directory.ResolveLinkTarget(path, true)?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool _ShouldExcludePath(string path)
    {
        return path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part =>
                JavaConsts.ExcludeFolderNames.Contains(part, StringComparer.OrdinalIgnoreCase));
    }


    /// <summary>
    ///     将 Java 版本规范化为统一比较格式（1.8.0 → 8.0.0）
    /// </summary>
    public static Version NormalizeVersion(Version version)
    {
        return version is { Major: 1, Minor: >= 0 }
            ? new Version(version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0))
            : version;
    }

    // ===== 版本处理工具 =====

    /// <summary>
    ///     检查版本是否在指定范围内（闭区间）
    /// </summary>
    public static bool IsVersionSuitable(
        Version javaVersion,
        Version minVersion,
        Version maxVersion)
    {
        var normalizedJava = NormalizeVersion(javaVersion);
        var normalizedMin = NormalizeVersion(minVersion);
        var normalizedMax = NormalizeVersion(maxVersion);

        return normalizedJava >= normalizedMin && normalizedJava <= normalizedMax;
    }
}