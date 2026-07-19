using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PCL;

/// <summary>
///     内嵌模组（Jar-in-Jar）解析结果的持久化缓存。每实例一个文件，位于该实例的
///     <c>PCL\JarInJar.json</c>（与 config.v1.yml 同级）。按 Mod 文件路径 + (最后修改时间, 大小) 指纹判断
///     有效性，避免每次加载都重新递归解析嵌套 jar；也为后续依赖/级联分析提供可查询的内嵌索引。
///     使用前须先 <see cref="UseInstance" /> 切到目标实例，用毕 <see cref="Flush" /> 落盘。
/// </summary>
public static class ModJarInJarCache
{
    /// <summary>缓存数据结构变化时递增此值以令旧缓存失效（改动 JIJ 解析/节点字段后务必升此值）。</summary>
    private const int FormatVersion = 2;

    private static readonly object _lock = new();
    private static string _cachePath; // 当前实例的缓存文件；null 表示未启用缓存
    private static Dictionary<string, CacheEntry> _entries;
    private static bool _dirty;

    public class CacheEntry
    {
        public long LastModified { get; set; }
        public long Size { get; set; }
        public List<EmbeddedModNode> Tree { get; set; } = new();
    }

    private class CacheFile
    {
        public int Version { get; set; }
        public Dictionary<string, CacheEntry> Entries { get; set; } = new();
    }

    /// <summary>
    ///     切换到某实例的缓存文件（<paramref name="instancePath" />\PCL\JarInJar.json，与 config.v1.yml 同级）；
    ///     会先落盘上一个实例的变更。传空表示停用缓存（此时解析不走缓存）。
    /// </summary>
    public static void UseInstance(string instancePath)
    {
        lock (_lock)
        {
            var newPath = string.IsNullOrEmpty(instancePath)
                ? null
                : Path.Combine(instancePath, "PCL", "JarInJar.json");
            if (string.Equals(_cachePath, newPath, StringComparison.OrdinalIgnoreCase)) return;
            _FlushLocked();
            _cachePath = newPath;
            _entries = null; // 换实例后惰性重载
        }
    }

    private static void _EnsureLoaded()
    {
        if (_entries is not null) return;
        _entries = new Dictionary<string, CacheEntry>();
        if (_cachePath is null) return;
        try
        {
            if (File.Exists(_cachePath))
            {
                var file = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(_cachePath));
                if (file is not null && file.Version == FormatVersion)
                    _entries = file.Entries ?? new Dictionary<string, CacheEntry>();
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "读取 Jar-in-Jar 缓存失败，已重置", ModBase.LogLevel.Developer);
        }
    }

    /// <summary>指纹匹配时返回缓存的内嵌树，否则返回 null。</summary>
    public static List<EmbeddedModNode> TryGet(string path, long lastModified, long size)
    {
        lock (_lock)
        {
            if (_cachePath is null) return null;
            _EnsureLoaded();
            if (_entries.TryGetValue(path, out var e) && e.LastModified == lastModified && e.Size == size)
                return e.Tree;
            return null;
        }
    }

    public static void Set(string path, long lastModified, long size, List<EmbeddedModNode> tree)
    {
        lock (_lock)
        {
            if (_cachePath is null) return;
            _EnsureLoaded();
            _entries[path] = new CacheEntry { LastModified = lastModified, Size = size, Tree = tree };
            _dirty = true;
        }
    }

    /// <summary>将变更原子写入磁盘（临时文件 + 移动）。批量加载结束后调用一次即可。</summary>
    public static void Flush()
    {
        lock (_lock)
        {
            _FlushLocked();
        }
    }

    private static void _FlushLocked()
    {
        if (!_dirty || _entries is null || _cachePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            var tmp = _cachePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new CacheFile { Version = FormatVersion, Entries = _entries }));
            if (File.Exists(_cachePath)) File.Delete(_cachePath);
            File.Move(tmp, _cachePath);
            _dirty = false;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "写入 Jar-in-Jar 缓存失败", ModBase.LogLevel.Developer);
        }
    }
}

/// <summary>内嵌模组的轻量序列化节点（供缓存与后续依赖分析使用）。</summary>
public class EmbeddedModNode
{
    public string FileName { get; set; }
    public string ModId { get; set; }
    public string Name { get; set; }
    public string Version { get; set; }

    /// <summary>声明的加载器（Fabric/Quilt/Forge/NeoForge）。</summary>
    public string Loader { get; set; }

    /// <summary>声明的目标 Minecraft 版本范围。</summary>
    public string TargetMcVersion { get; set; }

    public List<EmbeddedModNode> Children { get; set; } = new();
}
