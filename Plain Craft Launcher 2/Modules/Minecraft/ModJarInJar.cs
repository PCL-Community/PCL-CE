using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;

namespace PCL;

/// <summary>
///     Jar-in-Jar（内嵌模组）解析。
/// </summary>
public static class ModJarInJar
{
    private const int MaxDepth = 5;
    private const int MaxNodes = 512;

    /// <summary>
    ///     解析 <paramref name="jar" /> 内嵌套的其它 Mod jar，返回内嵌 Mod 列表。
    /// </summary>
    public static List<ModLocalComp.LocalCompFile> Resolve(string parentPath, ZipArchive jar, int depth = 0)
        => _Resolve(parentPath, jar, depth, new[] { MaxNodes });

    /// <summary>
    ///     带持久化缓存的解析：按文件指纹命中缓存则直接重建，否则解析并写入缓存（批量结束后需调用
    ///     <see cref="ModJarInJarCache.Flush" /> 落盘）。
    /// </summary>
    public static List<ModLocalComp.LocalCompFile> ResolveCached(string modFilePath, ZipArchive jar)
    {
        long lastModified, size;
        try
        {
            var fi = new FileInfo(modFilePath);
            lastModified = fi.LastWriteTimeUtc.Ticks;
            size = fi.Length;
        }
        catch
        {
            return Resolve(modFilePath, jar);
        }

        var cached = ModJarInJarCache.TryGet(modFilePath, lastModified, size);
        if (cached is not null) return _FromNodes(cached, modFilePath);

        var tree = Resolve(modFilePath, jar);
        ModJarInJarCache.Set(modFilePath, lastModified, size, _ToNodes(tree));
        return tree;
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
            Children = _ToNodes(m.EmbeddedMods)
        }).ToList();

    private static List<ModLocalComp.LocalCompFile> _FromNodes(List<EmbeddedModNode> nodes, string parentPath)
    {
        var result = new List<ModLocalComp.LocalCompFile>();
        foreach (var node in nodes)
        {
            var childPath = parentPath + "!/" + node.FileName;
            var child = new ModLocalComp.LocalCompFile(childPath);
            child.SetJijMetadata(node.Name, node.ModId, node.Version);
            child.JijLoader = node.Loader;
            child.JijTargetMcVersion = node.TargetMcVersion;
            child.EmbeddedMods = _FromNodes(node.Children, childPath);
            result.Add(child);
        }

        return result;
    }

    private static List<ModLocalComp.LocalCompFile> _Resolve(string parentPath, ZipArchive jar, int depth, int[] budget)
    {
        var result = new List<ModLocalComp.LocalCompFile>();
        if (depth >= MaxDepth) return result;

        var nestedPaths = new List<string>();
        _CollectFabricNestedJars(jar, nestedPaths);
        _CollectQuiltNestedJars(jar, nestedPaths);
        _CollectForgeNestedJars(jar, nestedPaths);
        _CollectManifestEmbeddedJars(jar, nestedPaths);

        foreach (var nestedPath in nestedPaths.Distinct())
        {
            if (budget[0] <= 0) break; // 总节点预算，避免病态嵌套爆树
            var entry = jar.GetEntry(nestedPath);
            if (entry is null) continue;
            budget[0]--;

            var childPath = parentPath + "!/" + nestedPath;
            var child = new ModLocalComp.LocalCompFile(childPath);
            child.MarkLoaded();
            // 始终列出该内嵌项：即使下方元数据解析/递归失败，也能按文件名保留（不丢节点）
            result.Add(child);

            try
            {
                using var ms = new MemoryStream();
                using (var es = entry.Open()) es.CopyTo(ms);
                ms.Position = 0;
                using var nestedJar = new ZipArchive(ms, ZipArchiveMode.Read);
                child.LookupMetadata(nestedJar);
                child.JijLoader = _DetectLoader(nestedJar);
                child.JijTargetMcVersion = child.Dependencies.TryGetValue("minecraft", out var mc) ? mc : null;
                child.EmbeddedMods = _Resolve(childPath, nestedJar, depth + 1, budget);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "解析内嵌 Mod 失败（" + parentPath + " -> " + nestedPath + "）", ModBase.LogLevel.Developer);
            }
        }

        return result;
    }

    private static void _CollectFabricNestedJars(ZipArchive jar, List<string> paths)
    {
        try
        {
            var entry = jar.GetEntry("fabric.mod.json");
            if (entry is null) return;
            var obj = (JsonObject)ModBase.GetJson(ModBase.ReadFile(entry.Open()));
            if (obj.TryGetPropertyValue("jars", out var jars) && jars is JsonArray arr)
                foreach (var j in arr)
                    if (j is JsonObject jo && jo.TryGetPropertyValue("file", out var file) && file is not null)
                        paths.Add(file.ToString());
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 fabric.mod.json 内嵌清单失败", ModBase.LogLevel.Developer);
        }
    }

    private static void _CollectForgeNestedJars(ZipArchive jar, List<string> paths)
    {
        try
        {
            var entry = jar.GetEntry("META-INF/jarjar/metadata.json");
            if (entry is null) return;
            var obj = (JsonObject)ModBase.GetJson(ModBase.ReadFile(entry.Open()));
            if (obj.TryGetPropertyValue("jars", out var jars) && jars is JsonArray arr)
                foreach (var j in arr)
                    if (j is JsonObject jo && jo.TryGetPropertyValue("path", out var p) && p is not null)
                        paths.Add(p.ToString());
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 META-INF/jarjar/metadata.json 内嵌清单失败", ModBase.LogLevel.Developer);
        }
    }

    // Quilt：quilt.mod.json 的 quilt_loader.jars（字符串数组，直接为内嵌 jar 路径）
    private static void _CollectQuiltNestedJars(ZipArchive jar, List<string> paths)
    {
        try
        {
            var entry = jar.GetEntry("quilt.mod.json");
            if (entry is null) return;
            var obj = (JsonObject)ModBase.GetJson(ModBase.ReadFile(entry.Open()));
            if (obj.TryGetPropertyValue("quilt_loader", out var ql) && ql is JsonObject qlo
                && qlo.TryGetPropertyValue("jars", out var jars) && jars is JsonArray arr)
                foreach (var j in arr)
                {
                    var s = j?.ToString();
                    if (!string.IsNullOrEmpty(s)) paths.Add(s);
                }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 quilt.mod.json 内嵌清单失败", ModBase.LogLevel.Developer);
        }
    }

    // JAR manifest 的 Embedded-Dependencies-Mod：无 mods.toml 的“包装 jar”仅通过它声明内嵌 mod
    private static void _CollectManifestEmbeddedJars(ZipArchive jar, List<string> paths)
    {
        try
        {
            var entry = jar.GetEntry("META-INF/MANIFEST.MF");
            if (entry is null) return;
            foreach (var raw in ModBase.ReadFile(entry.Open()).Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (!line.StartsWith("Embedded-Dependencies-Mod:", StringComparison.OrdinalIgnoreCase)) continue;
                var value = line.Substring("Embedded-Dependencies-Mod:".Length).Trim();
                if (!string.IsNullOrEmpty(value)) paths.Add(value);
                return;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "解析 MANIFEST.MF 内嵌声明失败", ModBase.LogLevel.Developer);
        }
    }

    private static string _DetectLoader(ZipArchive jar)
    {
        if (jar.GetEntry("fabric.mod.json") is not null) return "Fabric";
        if (jar.GetEntry("quilt.mod.json") is not null) return "Quilt";
        if (jar.GetEntry("META-INF/neoforge.mods.toml") is not null) return "NeoForge";
        if (jar.GetEntry("META-INF/mods.toml") is not null) return "Forge";
        if (jar.GetEntry("mcmod.info") is not null) return "Forge";
        return null;
    }
}
