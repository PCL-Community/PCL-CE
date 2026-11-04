using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

/// <summary>
///     把加载器生成的版本 Json 合并进实例 Json。
///     合并结果去掉 <c>inheritsFrom</c> 与 <c>jar</c>，并把 <c>id</c> 改回实例名，
///     使实例不依赖外部原版版本文件夹；核心 Jar 保持实例目录内的同名文件。
/// </summary>
internal static class InstanceJsonMerge
{
    public static void Merge(string instanceFolder, string instanceName, string loaderJsonPath)
    {
        var jsonPath = Path.Combine(instanceFolder, $"{instanceName}.json");
        var vanillaJson = ReadJson(jsonPath, Lang.Text("Minecraft.Version.Vanilla"));
        var loaderJson = ReadJson(loaderJsonPath, loaderJsonPath);

        loaderJson.Remove("releaseTime");
        loaderJson.Remove("time");

        var arguments = SplitMergeArguments(
            GetMinecraftArguments(vanillaJson) + " " + GetMinecraftArguments(loaderJson));
        vanillaJson.Merge(loaderJson);
        RemoveConflictingLwjgl2(vanillaJson);

        var realArguments = string.Join(" ", arguments.Distinct());
        if (!string.IsNullOrEmpty(realArguments.Replace(" ", "")))
            vanillaJson["minecraftArguments"] = realArguments;

        vanillaJson.Remove("_comment_");
        vanillaJson.Remove("inheritsFrom");
        vanillaJson.Remove("jar");
        vanillaJson["id"] = instanceName;

        LauncherCommon.WriteFile(jsonPath, vanillaJson.ToString(), new UTF8Encoding(false));
        LauncherCommon.Log($"[Install] 已合并实例 Json：{jsonPath}");
    }

    public static void Normalize(string instanceFolder, string instanceName)
    {
        var jsonPath = Path.Combine(instanceFolder, $"{instanceName}.json");
        var json = ReadJson(jsonPath, Lang.Text("Minecraft.Version.Vanilla"));
        json.Remove("_comment_");
        json.Remove("inheritsFrom");
        json.Remove("jar");
        json["id"] = instanceName;
        LauncherCommon.WriteFile(jsonPath, json.ToString(), new UTF8Encoding(false));
        LauncherCommon.Log($"[Install] 已规范化原版实例 Json：{jsonPath}");
    }

    private static JsonObject ReadJson(string path, string sourceName)
    {
        var text = LauncherCommon.ReadFile(path).TrimStart('\uFEFF');
        if (!text.TrimStart().StartsWith('{'))
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.JsonInvalid",
                    sourceName,
                    path,
                    text[..Math.Min(text.Length, 1000)]));
        return (JsonObject)LauncherCommon.GetJson(text);
    }

    private static string GetMinecraftArguments(JsonObject? json)
    {
        return (json?["minecraftArguments"] ?? " ").ToString();
    }

    private static List<string> SplitMergeArguments(string allArguments)
    {
        var rawArguments = allArguments
            .Split(" ")
            .Where(part => !string.IsNullOrEmpty(part))
            .Select(part => part.Trim())
            .ToList();
        var splitArguments = new List<string>();
        foreach (var argument in rawArguments)
            if (argument.StartsWith('-'))
                splitArguments.Add(argument);
            else if (splitArguments.Count != 0
                     && splitArguments[^1].StartsWith('-')
                     && !splitArguments[^1].Contains(' '))
                splitArguments[^1] = $"{splitArguments[^1]} {argument}";
            else
                splitArguments.Add(argument);

        return splitArguments;
    }

    private static void RemoveConflictingLwjgl2(JsonObject outputJson)
    {
        if (outputJson["libraries"] is not JsonArray libraries
            || !libraries.Any(IsLwjgl3Library))
            return;

        for (var i = libraries.Count - 1; i >= 0; i--)
            if (IsLwjgl2Library(libraries[i]))
            {
                LauncherCommon.Log($"[Install] 已移除与 LWJGL3 冲突的 LWJGL2 支持库：{libraries[i]?["name"]}");
                libraries.RemoveAt(i);
            }
    }

    private static bool IsLwjgl3Library(JsonNode? library)
    {
        if (library is not JsonObject lib)
            return false;
        var segments = (lib["name"]?.ToString() ?? "").Split(':');
        return segments is ["org.lwjgl", _, _, ..]
            && segments[2].StartsWith("3.", StringComparison.Ordinal);
    }

    private static bool IsLwjgl2Library(JsonNode? library)
    {
        if (library is not JsonObject lib)
            return false;
        var segments = (lib["name"]?.ToString() ?? "").Split(':');
        return segments.Length >= 3
            && (segments[0] == "org.lwjgl.lwjgl"
                || (segments[0] == "org.lwjgl"
                    && segments[2].StartsWith("2.", StringComparison.Ordinal)));
    }
}