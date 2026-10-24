using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;

namespace PCL;

/// <summary>Jar-in-Jar（内嵌模组）解析。</summary>
public static class ModJarInJar
{
    internal const int MaxDepth = 5;
    internal const int MaxNodes = 512;
    private const long MaxEntryBytes = 256L * 1024 * 1024;
    private const long MaxTotalBytes = 512L * 1024 * 1024;
    private const long MaxScanBytes = 2L * 1024 * 1024 * 1024;

    internal sealed class ScanContext
    {
        private long _bytesRemaining = MaxScanBytes;
        private readonly Func<bool>? _isCancelled;

        public ScanContext(Func<bool>? isCancelled = null) => _isCancelled = isCancelled;
        public bool IsCancelled => _isCancelled?.Invoke() == true;
        public bool TryConsume(int count) => !IsCancelled && Interlocked.Add(ref _bytesRemaining, -count) >= 0;
    }

    private sealed class _ResolveBudget
    {
        public int NodesRemaining = MaxNodes;
        public long BytesRemaining = MaxTotalBytes;
        public bool Incomplete;
        public ScanContext? Scan;
        public string? PreferredLoader;
    }

    private sealed class _NestedJarInfo
    {
        public string Path;
        public string Identifier;
        public string VersionRange;
        public string ArtifactVersion;
    }

    /// <summary>
    ///     带持久化缓存的解析：按文件指纹命中缓存则直接重建，否则解析并写入缓存（批量结束后需调用
    ///     <see cref="ModJarInJarCache.Flush" /> 落盘）。
    /// </summary>
    internal static List<ModLocalComp.LocalCompFile> ResolveCached(string modFilePath, ZipArchive jar,
        out bool complete, bool deferOnMiss = false,
        long? expectedLastModified = null, long? expectedSize = null, ScanContext? scan = null,
        string? preferredLoader = null)
    {
        complete = true;
        long lastModified, size;
        try
        {
            var fi = new FileInfo(modFilePath);
            lastModified = expectedLastModified ?? fi.LastWriteTimeUtc.Ticks;
            size = expectedSize ?? fi.Length;
            if (!_FingerprintMatches(fi, lastModified, size))
            {
                complete = false;
                return null;
            }
        }
        catch
        {
            if (deferOnMiss)
            {
                complete = false;
                return null;
            }
            var fallbackBudget = new _ResolveBudget { Scan = scan, PreferredLoader = preferredLoader };
            var fallback = _Resolve(modFilePath, jar, 0, fallbackBudget);
            complete = !fallbackBudget.Incomplete;
            return fallback;
        }

        var cached = ModJarInJarCache.TryGet(modFilePath, lastModified, size);
        if (cached is not null) return _FromNodes(cached, modFilePath);
        // 缓存未命中且要求延后：返回 null 交由列表加载完成后的后台线程补解析，
        // 不在首屏同步解压嵌套 jar（冷缓存下几百个 mod 的递归解压会拖慢列表出现）
        if (deferOnMiss)
        {
            complete = false;
            return null;
        }

        var budget = new _ResolveBudget { Scan = scan, PreferredLoader = preferredLoader };
        var tree = _Resolve(modFilePath, jar, 0, budget);
        complete = !budget.Incomplete;
        try
        {
            if (!_FingerprintMatches(new FileInfo(modFilePath), lastModified, size)) complete = false;
        }
        catch
        {
            complete = false;
        }
        // 截断/失败树不入盘：宿主指纹不变会被永久复用，缺失的 id 永不再现；下次启动重扫
        if (complete)
            try
            {
                ModJarInJarCache.Set(modFilePath, lastModified, size, _ToNodes(tree));
            }
            catch (Exception ex)
            {
                complete = false;
                ModBase.Log(ex, "写入 Jar-in-Jar 缓存条目失败", ModBase.LogLevel.Developer);
            }
        return tree;
    }

    private static bool _FingerprintMatches(FileInfo file, long lastModified, long size)
    {
        file.Refresh();
        return file.Exists && file.LastWriteTimeUtc.Ticks == lastModified && file.Length == size;
    }

    private static List<EmbeddedModNode> _ToNodes(List<ModLocalComp.LocalCompFile> mods)
        => mods.Select(m => new EmbeddedModNode
        {
            FileName = m.FileName,
            Name = m.Name,
            ModId = m.ModId,
            Version = m.Version,
            Loader = m.JijLoader,
            TargetMcVersion = m.JijTargetMcVersion,
            DependencyRows = m.DependencyDeclarations.Select(d => new EmbeddedDependency
                { Id = d.Id, Raw = d.Raw, Optional = d.Optional }).ToList(),
            Conflicts = m.ConflictDeclarations.Select(c => new EmbeddedConflict
                { DeclarerId = c.DeclarerId, Target = c.Target, Raw = c.Raw, Hard = c.Hard }).ToList(),
            ProvidedIds = m.ProvidedIds.ToList(),
            ProvidedVersions = new Dictionary<string, string>(m.ProvidedVersions),
            JijIdentifier = m.JijIdentifier,
            JijVersionRange = m.JijVersionRange,
            JijArtifactVersion = m.JijArtifactVersion,
            Children = _ToNodes(m.EmbeddedMods)
        }).ToList();

    private static List<ModLocalComp.LocalCompFile> _FromNodes(List<EmbeddedModNode> nodes, string parentPath)
    {
        var result = new List<ModLocalComp.LocalCompFile>();
        if (nodes is null) return result;
        foreach (var node in nodes)
        {
            var childPath = parentPath + "!/" + node.FileName;
            var child = new ModLocalComp.LocalCompFile(childPath);
            child.SetJijMetadata(node.Name, node.ModId, node.Version);
            child.JijLoader = node.Loader;
            child.JijTargetMcVersion = node.TargetMcVersion;
            child.JijIdentifier = node.JijIdentifier;
            child.JijVersionRange = node.JijVersionRange;
            child.JijArtifactVersion = node.JijArtifactVersion;
            child.SetJijDependencies(node.DependencyRows);
            child.SetJijConflicts(node.Conflicts, node.ProvidedIds, node.ProvidedVersions);
            child.EmbeddedMods = _FromNodes(node.Children, childPath);
            result.Add(child);
        }

        return result;
    }

    private static List<ModLocalComp.LocalCompFile> _Resolve(string parentPath, ZipArchive jar, int depth,
        _ResolveBudget budget)
    {
        var result = new List<ModLocalComp.LocalCompFile>();
        if (budget.Scan?.IsCancelled == true)
        {
            budget.Incomplete = true;
            return result;
        }
        var nested = new List<_NestedJarInfo>();
        if (!_CollectFabricNestedJars(jar, nested)) budget.Incomplete = true;
        if (!_CollectQuiltNestedJars(jar, nested)) budget.Incomplete = true;
        if (!_CollectForgeNestedJars(jar, nested)) budget.Incomplete = true;
        if (!_CollectManifestEmbeddedJars(jar, nested)) budget.Incomplete = true;
        nested = nested.Where(n => !string.IsNullOrWhiteSpace(n.Path))
            .GroupBy(n => n.Path, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(n => n.Identifier is not null).First())
            .ToList();
        if (depth >= MaxDepth)
        {
            if (nested.Count > 0) budget.Incomplete = true;
            return result;
        }

        foreach (var info in nested)
        {
            if (budget.Scan?.IsCancelled == true)
            {
                budget.Incomplete = true;
                break;
            }
            if (budget.NodesRemaining <= 0)
            {
                budget.Incomplete = true;
                break;
            }

            var entry = jar.GetEntry(info.Path);
            if (entry is null) continue;
            try
            {
                _ = ModBase.GetFileNameFromPath(info.Path);
            }
            catch (Exception ex)
            {
                budget.Incomplete = true;
                ModBase.Log(ex, "跳过路径无效的内嵌 Mod（" + parentPath + " -> " + info.Path + "）",
                    ModBase.LogLevel.Developer);
                continue;
            }
            if (entry.Length > MaxEntryBytes || entry.Length > budget.BytesRemaining)
            {
                budget.Incomplete = true;
                ModBase.Log("跳过过大的内嵌 Mod（" + parentPath + " -> " + info.Path + "，" + entry.Length +
                            " bytes）", ModBase.LogLevel.Developer);
                continue;
            }
            budget.NodesRemaining--;

            var childPath = parentPath + "!/" + info.Path;
            var child = new ModLocalComp.LocalCompFile(childPath);
            child.MarkLoaded();
            child.JijIdentifier = info.Identifier;
            child.JijVersionRange = info.VersionRange;
            child.JijArtifactVersion = info.ArtifactVersion;
            // 先加入结果，元数据或递归解析失败时仍保留该文件。
            result.Add(child);

            string tmp = null;
            try
            {
                // 嵌套流不可 seek，落临时文件后再作为 zip 打开，避免大嵌套 jar 全量进内存
                tmp = Path.GetTempFileName();
                using (var es = entry.Open())
                using (var fs = File.Create(tmp))
                    _CopyWithBudget(es, fs, budget);
                using var nestedJar = ZipFile.OpenRead(tmp);
                child.JijLoader = DetectLoader(nestedJar, budget.PreferredLoader);
                child.PreferredLoader = budget.PreferredLoader;
                child.LookupMetadata(nestedJar, child.JijLoader);
                // 用原始约束（未 Maven 化），保留 Fabric semver 原貌供求值
                child.JijTargetMcVersion = child.DependencyRaw.TryGetValue("minecraft", out var mc) ? mc : null;
                child.EmbeddedMods = _Resolve(childPath, nestedJar, depth + 1, budget);
            }
            catch (OperationCanceledException)
            {
                budget.Incomplete = true;
                throw;
            }
            catch (Exception ex)
            {
                budget.Incomplete = true;
                ModBase.Log(ex, "解析内嵌 Mod 失败（" + parentPath + " -> " + info.Path + "）", ModBase.LogLevel.Developer);
            }
            finally
            {
                if (tmp is not null)
                    try { File.Delete(tmp); }
                    catch { /* 临时文件清理失败无妨 */ }
            }
        }

        return result;
    }

    private static void _CopyWithBudget(Stream input, Stream output, _ResolveBudget budget)
    {
        var buffer = new byte[81920];
        long entryBytes = 0;
        while (true)
        {
            var read = input.Read(buffer, 0, buffer.Length);
            if (read <= 0) return;
            entryBytes += read;
            budget.BytesRemaining -= read;
            if (budget.Scan?.IsCancelled == true)
            {
                budget.Incomplete = true;
                throw new OperationCanceledException("Jar-in-Jar scan cancelled");
            }
            if (entryBytes > MaxEntryBytes || budget.BytesRemaining < 0 ||
                budget.Scan is not null && !budget.Scan.TryConsume(read))
            {
                budget.Incomplete = true;
                throw new InvalidDataException("内嵌 Mod 解压体积超过安全上限");
            }
            output.Write(buffer, 0, read);
        }
    }

    private static bool _CollectFabricNestedJars(ZipArchive jar, List<_NestedJarInfo> found)
    {
        try
        {
            var entry = jar.GetEntry("fabric.mod.json");
            if (entry is null) return true;
            var obj = (JsonObject)ModBase.GetJson(ModBase.ReadFile(entry.Open()));
            if (obj.TryGetPropertyValue("jars", out var jars) && jars is JsonArray arr)
                foreach (var j in arr)
                    if (j is JsonObject jo && jo.TryGetPropertyValue("file", out var file) && file is not null)
                        found.Add(new _NestedJarInfo { Path = file.ToString() });
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 fabric.mod.json 内嵌清单失败", ModBase.LogLevel.Developer);
            return false;
        }
    }

    private static bool _CollectForgeNestedJars(ZipArchive jar, List<_NestedJarInfo> found)
    {
        try
        {
            var entry = jar.GetEntry("META-INF/jarjar/metadata.json");
            if (entry is null) return true;
            var obj = (JsonObject)ModBase.GetJson(ModBase.ReadFile(entry.Open()));
            if (obj.TryGetPropertyValue("jars", out var jars) && jars is JsonArray arr)
                foreach (var j in arr)
                    if (j is JsonObject jo && jo.TryGetPropertyValue("path", out var p) && p is not null)
                    {
                        var identifier = jo["identifier"] as JsonObject;
                        var version = jo["version"] as JsonObject;
                        var group = identifier?["group"]?.ToString();
                        var artifact = identifier?["artifact"]?.ToString();
                        found.Add(new _NestedJarInfo
                        {
                            Path = p.ToString(),
                            Identifier = string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(artifact)
                                ? null
                                : group + ":" + artifact,
                            VersionRange = version?["range"]?.ToString(),
                            ArtifactVersion = version?["artifactVersion"]?.ToString()
                        });
                    }
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 META-INF/jarjar/metadata.json 内嵌清单失败", ModBase.LogLevel.Developer);
            return false;
        }
    }

    private static bool _CollectQuiltNestedJars(ZipArchive jar, List<_NestedJarInfo> found)
    {
        try
        {
            var entry = jar.GetEntry("quilt.mod.json");
            if (entry is null) return true;
            var obj = (JsonObject)ModBase.GetJson(ModBase.ReadFile(entry.Open()));
            if (obj.TryGetPropertyValue("quilt_loader", out var ql) && ql is JsonObject qlo
                && qlo.TryGetPropertyValue("jars", out var jars) && jars is JsonArray arr)
                foreach (var j in arr)
                {
                    var s = j?.ToString();
                    if (!string.IsNullOrEmpty(s)) found.Add(new _NestedJarInfo { Path = s });
                }
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 quilt.mod.json 内嵌清单失败", ModBase.LogLevel.Developer);
            return false;
        }
    }

    // JAR manifest 的 Embedded-Dependencies-Mod：无 mods.toml 的“包装 jar”仅通过它声明内嵌 mod
    private static bool _CollectManifestEmbeddedJars(ZipArchive jar, List<_NestedJarInfo> found)
    {
        try
        {
            var entry = jar.GetEntry("META-INF/MANIFEST.MF");
            if (entry is null) return true;
            // JAR manifest 按 72 字节折行，续行以单个空格开头且无分隔符续接上一行；
            // 需先展开再解析，否则 Connector 等长内嵌路径会在续行处被截断而找不到条目
            var sb = new System.Text.StringBuilder();
            foreach (var raw in ModBase.ReadFile(entry.Open()).Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith(" ")) sb.Append(line, 1, line.Length - 1);
                else sb.Append('\n').Append(line);
            }

            foreach (var line in sb.ToString().Split('\n'))
            {
                if (!line.StartsWith("Embedded-Dependencies-Mod:", StringComparison.OrdinalIgnoreCase)) continue;
                var value = line.Substring("Embedded-Dependencies-Mod:".Length).Trim();
                if (!string.IsNullOrEmpty(value)) found.Add(new _NestedJarInfo { Path = value });
                return true;
            }
            return true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 MANIFEST.MF 内嵌声明失败", ModBase.LogLevel.Developer);
            return false;
        }
    }

    /// <summary>按存在的清单文件判断 jar 声明的加载器（Fabric/Quilt/Forge/NeoForge）；无从判断返回 null。</summary>
    public static string? DetectLoader(ZipArchive jar, string? preferredLoader = null)
    {
        var hasFabric = jar.GetEntry("fabric.mod.json") is not null;
        var hasQuilt = jar.GetEntry("quilt.mod.json") is not null;
        var hasNeoForge = jar.GetEntry("META-INF/neoforge.mods.toml") is not null;
        var hasForge = jar.GetEntry("META-INF/mods.toml") is not null || jar.GetEntry("mcmod.info") is not null;
        if (string.Equals(preferredLoader, "NeoForge", StringComparison.OrdinalIgnoreCase) && hasNeoForge)
            return "NeoForge";
        if (string.Equals(preferredLoader, "Forge", StringComparison.OrdinalIgnoreCase) && hasForge)
            return "Forge";
        if (string.Equals(preferredLoader, "Fabric", StringComparison.OrdinalIgnoreCase) && hasFabric)
            return "Fabric";
        if (string.Equals(preferredLoader, "Quilt", StringComparison.OrdinalIgnoreCase) && hasQuilt)
            return "Quilt";
        if (hasFabric) return "Fabric";
        if (hasQuilt) return "Quilt";
        if (hasNeoForge) return "NeoForge";
        if (hasForge) return "Forge";
        return null;
    }
}
