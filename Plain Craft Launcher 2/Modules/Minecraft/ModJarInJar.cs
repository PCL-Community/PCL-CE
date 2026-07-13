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

    /// <summary>
    ///     解析 <paramref name="jar" /> 内嵌套的其它 Mod jar，返回内嵌 Mod 列表。
    /// </summary>
    public static List<ModLocalComp.LocalCompFile> Resolve(string parentPath, ZipArchive jar, int depth = 0)
    {
        var result = new List<ModLocalComp.LocalCompFile>();
        if (depth >= MaxDepth) return result;

        var nestedPaths = new List<string>();
        _CollectFabricNestedJars(jar, nestedPaths);
        _CollectForgeNestedJars(jar, nestedPaths);

        foreach (var nestedPath in nestedPaths.Distinct())
        {
            try
            {
                var entry = jar.GetEntry(nestedPath);
                if (entry is null) continue;
                using var ms = new MemoryStream();
                using (var es = entry.Open()) es.CopyTo(ms);
                ms.Position = 0;
                using var nestedJar = new ZipArchive(ms, ZipArchiveMode.Read);

                var childPath = parentPath + "!/" + nestedPath;
                var child = new ModLocalComp.LocalCompFile(childPath);
                child.LookupMetadata(nestedJar);
                child.MarkLoaded(); 
                child.EmbeddedMods = Resolve(childPath, nestedJar, depth + 1);
                result.Add(child);
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
}
