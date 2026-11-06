using System.Collections;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PCL;

/// <summary>每实例的 Jar-in-Jar 解析缓存，使用文件路径、修改时间和大小作为指纹。</summary>
public static class JarInJarCache
{
    internal const int MaxDepth = 5;
    internal const int MaxNodes = 512;

    /// <summary>
    ///     仅在持久化结构、编码或现有字段语义不兼容时递增 Major 并将 Minor 归零；
    ///     只需强制重建缓存的解析逻辑调整递增 Minor。
    /// </summary>
    private const string FormatVersion = "8.3";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers =
            {
                _DropEmptyCollections
            }
        }
    };

    private static readonly Lock Lock = new();

    private static readonly Dictionary<string, Store> Stores =
        new(StringComparer.OrdinalIgnoreCase);

    [ThreadStatic] private static Store? _current;

    private static void _DropEmptyCollections(JsonTypeInfo info)
    {
        foreach (var prop in info.Properties)
            if (typeof(ICollection).IsAssignableFrom(prop.PropertyType))
                prop.ShouldSerialize = static (_, value) => value is ICollection { Count: > 0 };
    }

    /// <summary>
    ///     注册并切换到某实例的缓存（<paramref name="instancePath" />\PCL\JarInJar.bin）。
    ///     传空表示后续路由不到的读写不走缓存。
    /// </summary>
    public static void UseInstance(string? instancePath)
    {
        lock (Lock)
        {
            if (string.IsNullOrEmpty(instancePath))
            {
                _current = null;
                return;
            }

            var key = instancePath.TrimEnd('\\', '/');
            if (!Stores.TryGetValue(key, out var store))
            {
                store = new Store
                {
                    CachePath = Path.Combine(key, "PCL", "JarInJar.bin")
                };
                Stores[key] = store;
            }

            _current = store;
        }
    }

    private static string _NormalizeKey(string path)
    {
        return path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
            ? path[..^".disabled".Length]
            : path;
    }

    private static void _EnsureLoaded(Store store)
    {
        if (store.Entries is not null)
            return;

        store.Entries = new Dictionary<string, CacheEntry>();
        try
        {
            if (!File.Exists(store.CachePath))
                return;

            using var fs = File.OpenRead(store.CachePath);
            using var gz = new GZipStream(fs, CompressionMode.Decompress);
            using var doc = JsonDocument.Parse(gz);

            var root = doc.RootElement;

            if (!root.TryGetProperty("Version", out var ver)
                || ver.ValueKind != JsonValueKind.String
                || ver.GetString() != FormatVersion)
                return;

            if (!root.TryGetProperty("Entries", out var entries)
                || entries.ValueKind != JsonValueKind.Object)
                return;

            foreach (var prop in entries.EnumerateObject())
                try
                {
                    var entry = prop.Value.Deserialize<CacheEntry>();
                    if (entry is not null && _IsValidTree(entry.Tree))
                        store.Entries[prop.Name] = entry;
                }
                catch (Exception ex)
                {
                    LauncherCommon.Log(
                        ex,
                        $"跳过损坏的 Jar-in-Jar 缓存条目：{prop.Name}",
                        LauncherLogLevel.Developer);
                }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                "读取 Jar-in-Jar 缓存失败，已重置",
                LauncherLogLevel.Developer);
        }
    }

    private static bool _IsValidTree(List<EmbeddedModNode> tree)
    {
        if (tree is null)
            return false;

        var pending = new Stack<(List<EmbeddedModNode> Nodes, int Depth)>();
        pending.Push((tree, 1));
        var count = 0;
        while (pending.Count > 0)
        {
            var (nodes, depth) = pending.Pop();
            if (depth > MaxDepth)
                return false;

            foreach (var node in nodes)
            {
                if (node is null
                    || string.IsNullOrWhiteSpace(node.FileName)
                    || node.DependencyRows is null
                    || node.Conflicts is null
                    || node.ProvidedIds is null
                    || node.ProvidedVersions is null
                    || node.Children is null
                    || node.ProvidedIds.Any(string.IsNullOrWhiteSpace))
                    return false;

                if (++count > MaxNodes)
                    return false;
                if (node.Children.Count > 0)
                    pending.Push((node.Children, depth + 1));
            }
        }

        return true;
    }

    /// <summary>指纹匹配时返回缓存的内嵌树，否则返回 null。</summary>
    public static List<EmbeddedModNode>? TryGet(string path, long lastModified, long size)
    {
        var store = _current;
        if (store is null) return null;
        lock (Lock)
        {
            _EnsureLoaded(store);
            var entries = store.Entries
                ?? throw new InvalidOperationException("Jar-in-Jar 缓存未初始化");
            return entries.TryGetValue(_NormalizeKey(path), out var e)
                && e.LastModified == lastModified
                && e.Size == size
                    ? e.Tree
                    : null;
        }
    }

    public static void Set(
        string path,
        long lastModified,
        long size,
        List<EmbeddedModNode> tree)
    {
        var store = _current;
        if (store is null)
            return;

        lock (Lock)
        {
            _EnsureLoaded(store);
            var entries = store.Entries
                ?? throw new InvalidOperationException("Jar-in-Jar 缓存未初始化");
            entries[_NormalizeKey(path)] = new CacheEntry
            {
                LastModified = lastModified,
                Size = size,
                Tree = tree
            };
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
        lock (Lock)
        {
            var list = keepPaths as ICollection<string> ?? [.. keepPaths];
            _EnsureLoaded(store);
            var entries = store.Entries
                ?? throw new InvalidOperationException("Jar-in-Jar 缓存未初始化");
            var keep = new HashSet<string>(
                list.Select(_NormalizeKey),
                StringComparer.OrdinalIgnoreCase);
            var stale = entries
                .Keys
                .Where(k => !keep.Contains(k))
                .ToList();

            if (stale.Count == 0)
                return;

            foreach (var k in stale) entries.Remove(k);
            store.Dirty = true;
        }
    }

    /// <summary>将全部实例的变更原子写入磁盘（临时文件 + 移动）。批量加载结束后调用一次即可。</summary>
    public static void Flush()
    {
        lock (Lock)
        {
            foreach (var store in Stores.Values)
                _FlushStore(store);
        }
    }

    private static void _FlushStore(Store store)
    {
        if (!store.Dirty || store.Entries is null || store.CachePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(store.CachePath)!);
            var tmp = $"{store.CachePath}.tmp";
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                {
                    JsonSerializer.Serialize(
                        gz,
                        new CacheFile
                        {
                            Version = FormatVersion,
                            Entries = store.Entries
                        },
                        JsonOpts);
                }

                bytes = ms.ToArray();
            }

            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, store.CachePath, true);

            var legacy = Path.Combine(
                Path.GetDirectoryName(store.CachePath)!,
                "JarInJar.json");
            if (File.Exists(legacy)) File.Delete(legacy);
            store.Dirty = false;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "写入 Jar-in-Jar 缓存失败", LauncherLogLevel.Developer);
        }
    }

    private class Store
    {
        public string? CachePath;
        public bool Dirty;
        public Dictionary<string, CacheEntry>? Entries;
    }

    private class CacheEntry
    {
        public long LastModified { get; set; }
        public long Size { get; set; }
        public List<EmbeddedModNode> Tree { get; set; } = [];
    }

    private class CacheFile
    {
        public string? Version { get; set; }
        public Dictionary<string, CacheEntry> Entries { get; set; } = new();
    }
}