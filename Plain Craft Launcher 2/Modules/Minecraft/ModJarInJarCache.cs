using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PCL;

/// <summary>每实例的 Jar-in-Jar 解析缓存，使用文件路径、修改时间和大小作为指纹。</summary>
public static class ModJarInJarCache
{
    /// <summary>
    ///     仅在持久化结构、编码或现有字段语义不兼容时递增 Major 并将 Minor 归零；
    ///     只需强制重建缓存的解析逻辑调整递增 Minor。
    /// </summary>
    private const string FormatVersion = "8.3";

    // 缓存落盘为 gzip 压缩的紧凑 JSON，并省略空字段与空集合。
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { _DropEmptyCollections } }
    };

    private static void _DropEmptyCollections(JsonTypeInfo info)
    {
        foreach (var prop in info.Properties)
            if (typeof(ICollection).IsAssignableFrom(prop.PropertyType))
                prop.ShouldSerialize = static (_, value) => value is ICollection { Count: > 0 };
    }

    private static readonly object _lock = new();
    private static readonly Dictionary<string, _Store> _stores = new(StringComparer.OrdinalIgnoreCase);

    // 每线程各自的"当前实例"：列表加载线程与崩溃导出线程并发时互不干扰，
    // 避免一个线程 UseInstance 切走 _current 后，另一个线程路由回退写进错误实例的缓存
    [ThreadStatic] private static _Store? _current;

    private class _Store
    {
        public string CachePath;
        public Dictionary<string, CacheEntry> Entries; // null = 未加载
        public bool Dirty;
    }

    private class CacheEntry
    {
        public long LastModified { get; set; }
        public long Size { get; set; }

        public List<EmbeddedModNode> Tree { get; set; } = new();
    }

    private class CacheFile
    {
        public string Version { get; set; }
        public Dictionary<string, CacheEntry> Entries { get; set; } = new();
    }

    /// <summary>
    ///     注册并切换到某实例的缓存（<paramref name="instancePath" />\PCL\JarInJar.bin）。
    ///     传空表示后续路由不到的读写不走缓存。
    /// </summary>
    public static void UseInstance(string? instancePath)
    {
        lock (_lock)
        {
            if (string.IsNullOrEmpty(instancePath))
            {
                _current = null;
                return;
            }

            var key = instancePath.TrimEnd('\\', '/');
            if (!_stores.TryGetValue(key, out var store))
            {
                store = new _Store
                {
                    CachePath = Path.Combine(key, "PCL", "JarInJar.bin")
                };
                _stores[key] = store;
            }

            _current = store;
        }
    }

    // 启/禁用是纯改名、mtime 不变；剥 .disabled 后复用同键避免白白重扫。
    // 保留 .old（.old 可与新文件并存，剥了会键冲突）。
    private static string _NormalizeKey(string path)
    {
        return path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? path.Substring(0, path.Length - ".disabled".Length)
            : path;
    }

    private static void _EnsureLoaded(_Store store)
    {
        if (store.Entries is not null) return;
        store.Entries = new Dictionary<string, CacheEntry>();
        try
        {
            if (!File.Exists(store.CachePath)) return;
            using var fs = File.OpenRead(store.CachePath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var doc = JsonDocument.Parse(gz);
            var root = doc.RootElement;
            // 格式版本不符：丢整表（旧整数版本也会在此安全失效）
            if (!root.TryGetProperty("Version", out var ver) || ver.ValueKind != JsonValueKind.String ||
                ver.GetString() != FormatVersion) return;
            if (!root.TryGetProperty("Entries", out var entries) || entries.ValueKind != JsonValueKind.Object) return;
            // 逐条容错：单条损坏只丢那条，不丢整表
            foreach (var prop in entries.EnumerateObject())
                try
                {
                    var entry = prop.Value.Deserialize<CacheEntry>();
                    if (entry is not null && _IsValidTree(entry.Tree)) store.Entries[prop.Name] = entry;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "跳过损坏的 Jar-in-Jar 缓存条目：" + prop.Name, ModBase.LogLevel.Developer);
                }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "读取 Jar-in-Jar 缓存失败，已重置", ModBase.LogLevel.Developer);
        }
    }

    private static bool _IsValidTree(List<EmbeddedModNode> tree)
    {
        if (tree is null) return false;
        var pending = new Stack<(List<EmbeddedModNode> Nodes, int Depth)>();
        pending.Push((tree, 1));
        var count = 0;
        while (pending.Count > 0)
        {
            var (nodes, depth) = pending.Pop();
            if (depth > ModJarInJar.MaxDepth) return false;
            foreach (var node in nodes)
            {
                if (node is null || string.IsNullOrWhiteSpace(node.FileName) ||
                    node.DependencyRows is null || node.Conflicts is null || node.ProvidedIds is null ||
                    node.ProvidedVersions is null || node.Children is null ||
                    node.ProvidedIds.Any(string.IsNullOrWhiteSpace))
                    return false;
                if (++count > ModJarInJar.MaxNodes) return false;
                if (node.Children.Count > 0) pending.Push((node.Children, depth + 1));
            }
        }

        return true;
    }

    /// <summary>指纹匹配时返回缓存的内嵌树，否则返回 null。</summary>
    public static List<EmbeddedModNode> TryGet(string path, long lastModified, long size)
    {
        var store = _current;
        if (store is null) return null;
        lock (_lock)
        {
            _EnsureLoaded(store);
            if (store.Entries.TryGetValue(_NormalizeKey(path), out var e) && e.LastModified == lastModified &&
                e.Size == size)
                return e.Tree;
            return null;
        }
    }

    public static void Set(string path, long lastModified, long size, List<EmbeddedModNode> tree)
    {
        var store = _current;
        if (store is null) return;
        lock (_lock)
        {
            _EnsureLoaded(store);
            store.Entries[_NormalizeKey(path)] =
                new CacheEntry { LastModified = lastModified, Size = size, Tree = tree };
            store.Dirty = true;
        }
    }

    /// <summary>
    ///     清理当前实例存储中已不存在的文件条目（删除/改名后残留），<paramref name="keepPaths" /> 为本次
    ///     扫描到的全部 Mod 文件路径。应由模组列表加载器在扫描完成后调用。
    /// </summary>
    public static void Prune(IEnumerable<string> keepPaths)
    {
        var store = _current;
        if (store is null) return;
        lock (_lock)
        {
            var list = keepPaths as ICollection<string> ?? keepPaths.ToList();
            _EnsureLoaded(store);
            var keep = new HashSet<string>(list.Select(_NormalizeKey), StringComparer.OrdinalIgnoreCase);
            var stale = store.Entries.Keys.Where(k => !keep.Contains(k)).ToList();
            if (stale.Count == 0) return;
            foreach (var k in stale) store.Entries.Remove(k);
            store.Dirty = true;
        }
    }

    /// <summary>将全部实例的变更原子写入磁盘（临时文件 + 移动）。批量加载结束后调用一次即可。</summary>
    public static void Flush()
    {
        lock (_lock)
        {
            foreach (var store in _stores.Values)
                _FlushStore(store);
        }
    }

    private static void _FlushStore(_Store store)
    {
        if (!store.Dirty || store.Entries is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(store.CachePath)!);
            var tmp = store.CachePath + ".tmp";
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                    JsonSerializer.Serialize(gz,
                        new CacheFile { Version = FormatVersion, Entries = store.Entries }, _jsonOpts);
                bytes = ms.ToArray();
            }

            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, store.CachePath, true);
            // 清理旧版明文缓存（.json → .bin 迁移后残留）
            var legacy = Path.Combine(Path.GetDirectoryName(store.CachePath)!, "JarInJar.json");
            if (File.Exists(legacy)) File.Delete(legacy);
            store.Dirty = false;
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

    public string Loader { get; set; }

    public string TargetMcVersion { get; set; }

    /// <summary>本内嵌 mod 的全部依赖声明；同一 ID 的不同范围/可选性分别保留。</summary>
    public List<EmbeddedDependency> DependencyRows { get; set; } = new();

    /// <summary>本内嵌 mod 声明的冲突关系（对方 ModId + 生效版本约束 + 是否硬冲突）。</summary>
    public List<EmbeddedConflict> Conflicts { get; set; } = new();

    /// <summary>本内嵌 mod 额外提供的别名 id（multi-mod 兄弟 / Fabric provides）。</summary>
    public List<string> ProvidedIds { get; set; } = new();

    /// <summary>具有独立版本的别名（主要是 Forge/NeoForge multi-mod JAR 的兄弟 mod）。</summary>
    public Dictionary<string, string> ProvidedVersions { get; set; } = new();

    public string JijIdentifier { get; set; }

    public string JijVersionRange { get; set; }

    public string JijArtifactVersion { get; set; }

    public List<EmbeddedModNode> Children { get; set; } = new();
}

public class EmbeddedDependency
{
    public string Id { get; set; }
    public string Raw { get; set; }
    public bool Optional { get; set; }
}

public class EmbeddedConflict
{
    public string DeclarerId { get; set; }
    public string Target { get; set; }
    public string Raw { get; set; }
    public bool Hard { get; set; }
}
