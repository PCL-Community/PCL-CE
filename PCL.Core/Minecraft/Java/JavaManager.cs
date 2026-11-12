using PCL.Core.Logging;
using PCL.Core.Minecraft.Java.Parser;
using PCL.Core.Minecraft.Java.Scanner;
using PCL.Core.App;
using PCL.Core.Utils.Exts;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Java;

public class JavaManager(
    IJavaParser parser,
    params IJavaScanner[] scanners)
{
    private const string ModuleName = "JavaManager";
    private readonly Dictionary<string, JavaEntry> _javaEntrys = [];

    private readonly IJavaParser _parser = parser;
    private readonly IJavaScanner[] _scanners = scanners;

    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private DateTime _lastScanTime = DateTime.MinValue;
    private static readonly TimeSpan _MinScanInterval = TimeSpan.FromSeconds(13);

    public void SaveConfig()
    {
        try
        {
            var items = _javaEntrys
                .Select(x => new JavaStorageItem()
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
            var items = JsonSerializer.Deserialize<JavaStorageItem[]>(States.Game.JavaList, JsonCompat.SerializerOptions);
            if (items is null) return;

            var itemsAdded = new List<JavaEntry>();

            foreach (var item in items)
            {
                var parserResult = _parser.Parse(item.Path);
                parserResult.Match(
                    inst =>
                    {
                        itemsAdded.Add(new JavaEntry()
                        {
                            Installation = inst,
                            IsEnabled = item.IsEnable,
                            Source = item.Source ?? JavaSource.AutoScanned
                        });
                        return 0;
                    },
                    err =>
                    {
                        LogWrapper.Trace(ModuleName, $"Can not find Java {item.Path}, skip");
                        return 0;
                    }
                );
            }

            lock (_javaEntrys)
            {
                foreach (var item in itemsAdded)
                {
                    if (_javaEntrys.TryGetValue(item.Installation.JavaExePath, out var existingRecord))
                    {
                        existingRecord.IsEnabled = item.IsEnabled;
                        existingRecord.Source = item.Source;
                    }
                    else
                    {
                        _javaEntrys.Add(item.Installation.JavaExePath, item);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, "无法读取 Java 配置项");
        }
    }

    /// <summary>
    /// 扫描 Java 安装
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

        bool ShouldSkip()
        {
            return !force && (DateTime.Now - _lastScanTime) < _MinScanInterval;
        }
    }

    private void _ScanInternal()
    {
        var pathSet = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        Parallel.ForEach(_scanners, scanner =>
        {
            var temp = new List<string>();
            scanner.Scan(temp);
            foreach (var path in temp)
            {
                var normalized = _NormalizePath(path);
                if (!_ShouldExcludePath(normalized))
                    pathSet.TryAdd(normalized, true);
            }
        });

        var parsedEntries = pathSet.Keys
            .Select(_parser.Parse);
        List<JavaEntry> scannedEntries = [];
        foreach (var entry in parsedEntries)
        {
            entry.Match(
                inst =>
                {
                    scannedEntries.Add(new JavaEntry
                    {
                        Installation = inst,
                        IsEnabled = _javaEntrys
                            .TryGetValue(_NormalizePath(inst.JavaExePath), out var existingJava)
                                ? existingJava.IsEnabled
                                : _ShouldEnableByDefault(inst),
                        Source = JavaSource.AutoScanned
                    });
                    return 0;
                },
                err =>
                {
                    LogWrapper.Warn(err, "Unable to parse Java file");
                    return 0;
                }
            );
        }

        lock (_javaEntrys)
        {
            foreach (var entry in scannedEntries)
            {
                _javaEntrys[entry.Installation.JavaExePath] = entry;
            }
        }
    }

    private static bool _ShouldEnableByDefault(JavaInstallation inst)
    {
        var libDir = Path.Combine(Directory.GetParent(inst.JavaFolder)!.FullName, "lib");
        var isUsable = (!inst.IsJre && File.Exists(Path.Combine(libDir, "jvm.lib"))) ||
                       (inst.IsJre && File.Exists(Path.Combine(libDir, "rt.jar")));

        return !((inst.IsJre && inst.MajorVersion > 8) ||
                 (inst.Is64Bit ^ Environment.Is64BitOperatingSystem) ||
                 !isUsable);
    }

    public List<JavaEntry> GetSortedJavaList()
    {
        var ret = _javaEntrys.Values.ToList();
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
        lock (_javaEntrys)
        {
            return _javaEntrys.Any(x => x.Value.Installation.Is64Bit);
        }
    }

    public bool ExistAnyJava()
    {
        return _javaEntrys.Count != 0;
    }

    public bool Exist(string javaExePath)
    {
        return _javaEntrys.ContainsKey(javaExePath);
    }

    /// <summary>
    /// 获取，如果没有就加入记录
    /// </summary>
    /// <param name="javaExePath"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public JavaEntry? AddOrGet(string javaExePath)
    {
        try
        {
            if (javaExePath.IsNullOrWhiteSpace() || !File.Exists(javaExePath)) return null;

            return _parser.Parse(javaExePath).Match<JavaEntry?>(
                inst =>
                {
                    var exePath = _NormalizePath(inst.JavaExePath);
                    lock (_javaEntrys)
                    {
                        if (_javaEntrys.TryGetValue(exePath, out var ret))
                            return ret;

                        var entry = new JavaEntry
                        {
                            Installation = inst,
                            IsEnabled = _ShouldEnableByDefault(inst),
                            Source = JavaSource.ManualAdded
                        };

                        _javaEntrys.Add(exePath, entry);
                        return entry;
                    }
                },
                err =>
                {
                    return null;
                }
            );
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, ModuleName, $"Failed to add or get {javaExePath}");
            return null;
        }
    }
    /// <summary>
    /// 仅获取，如果没有不增加记录
    /// </summary>
    public JavaEntry? Get(string javaExePath)
    {
        if (javaExePath.IsNullOrWhiteSpace() || !File.Exists(javaExePath)) return null;

        return _parser.Parse(javaExePath).Match<JavaEntry?>(
            inst =>
            {
                var exePath = _NormalizePath(inst.JavaExePath);
                lock (_javaEntrys)
                {
                    if (_javaEntrys.TryGetValue(exePath, out var ret))
                        return ret;

                    var entry = new JavaEntry
                    {
                        Installation = inst,
                        IsEnabled = _ShouldEnableByDefault(inst),
                        Source = JavaSource.ManualAdded
                    };

                    return entry;
                }
            },
            err =>
            {
                return null;
            }
        );
    }

    public async Task<JavaEntry[]> SelectSuitableJavaAsync(Version minVersion, Version maxVersion)
    {
        if (_javaEntrys.Count == 0)
            await ScanJavaAsync();

        lock (_javaEntrys)
        {
            return _javaEntrys
                .Values.ToList()
                .Where(j => j.Installation.IsStillAvailable && j.IsEnabled &&
                            IsVersionSuitable(j.Installation.Version, minVersion, maxVersion))
                .OrderBy(static j => j.Installation.MajorVersion) // 确保首要选择的大版本正确
                .ThenBy(static j => j.Installation.IsJre) // JDK 优先
                .ThenBy(static j => j.Installation.Brand) // Java 发行版优选
                .ThenByDescending(static j => j.Installation.Version) // 优选后小版本号较高的版本
                .ToArray();
        }
    }

    public void CheckAllAvailability()
    {
        lock (_javaEntrys)
        {
            var keys4Remove = _javaEntrys
                .Where(kv => !kv.Value.Installation.IsStillAvailable)
                .Select(kv => kv.Key)
                .ToArray();
            foreach (var key in keys4Remove)
                _javaEntrys.Remove(key);
        }
    }

    // ===== 路径工具 =====
    private static string _NormalizePath(string path) =>
        Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool _ShouldExcludePath(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => JavaConsts.ExcludeFolderNames.Contains(part, StringComparer.OrdinalIgnoreCase));


    /// <summary>
    /// 将 Java 版本规范化为统一比较格式（1.8.0 → 8.0.0）
    /// </summary>
    public static Version NormalizeVersion(Version version) =>
        version.Major == 1 && version.Minor >= 0
            ? new Version(version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0))
            : version;

    // ===== 版本处理工具 =====

    /// <summary>
    /// 检查版本是否在指定范围内（闭区间）
    /// </summary>
    public static bool IsVersionSuitable(Version javaVersion, Version minVersion, Version maxVersion)
    {
        var normalizedJava = NormalizeVersion(javaVersion);
        var normalizedMin = NormalizeVersion(minVersion);
        var normalizedMax = NormalizeVersion(maxVersion);

        return normalizedJava >= normalizedMin && normalizedJava <= normalizedMax;
    }
}
