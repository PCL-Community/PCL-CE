using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

/// <summary>参与合并的加载器 Json 种类。</summary>
internal enum InstallJsonKind
{
    OptiFine,
    Forge,
    NeoForge,
    Cleanroom,
    LiteLoader,
    Fabric,
    LegacyFabric,
    LabyMod
}

/// <summary>加载器 Json 与其种类。</summary>
internal sealed record InstallJsonSource(InstallJsonKind Kind, string Path);

/// <summary>供安装步骤共享的加载器 Json 列表。</summary>
internal sealed class InstallJsonSourceCollection
{
    private readonly List<InstallJsonSource> _sources = [];
    private readonly Lock _sync = new();

    public void Add(InstallJsonKind kind, string path)
    {
        lock (_sync)
        {
            _sources.RemoveAll(source => source.Kind == kind);
            _sources.Add(new InstallJsonSource(kind, path));
        }
    }

    public IReadOnlyList<InstallJsonSource> Snapshot()
    {
        lock (_sync)
        {
            return [.. _sources];
        }
    }
}

/// <summary>
///     把加载器生成的版本 Json 合并进实例 Json。
///     合并结果去掉 <c>inheritsFrom</c> 与 <c>jar</c>，并把 <c>id</c> 改回实例名，
///     使实例不依赖外部原版版本文件夹；核心 Jar 保持实例目录内的同名文件。
///     MMC 整合包的 overrides 会作为原版 Json 的补丁应用，被覆盖的加载器 Json 不再合并。
/// </summary>
internal static class InstanceJsonMerge
{
    private static readonly InstallJsonKind[] MergeOrder =
    [
        InstallJsonKind.OptiFine,
        InstallJsonKind.Forge,
        InstallJsonKind.NeoForge,
        InstallJsonKind.Cleanroom,
        InstallJsonKind.LiteLoader,
        InstallJsonKind.Fabric,
        InstallJsonKind.LegacyFabric,
        InstallJsonKind.LabyMod
    ];

    private static readonly InstallJsonKind[] ArgumentOrder =
    [
        InstallJsonKind.LabyMod,
        InstallJsonKind.OptiFine,
        InstallJsonKind.Forge,
        InstallJsonKind.NeoForge,
        InstallJsonKind.LiteLoader,
        InstallJsonKind.Cleanroom
    ];

    /// <summary>合并一个加载器 Json 并规范化实例 Json。保留给旧调用方使用。</summary>
    public static void Merge(string instanceFolder, string instanceName, string loaderJsonPath)
    {
        var kind = GuessKind(loaderJsonPath);
        MergeInstall(
            instanceFolder,
            instanceName,
            [new InstallJsonSource(kind, loaderJsonPath)],
            null,
            null);
    }

    /// <summary>
    ///     合并全部加载器 Json。顺序与 WPF MergeJson 一致：OptiFine → Forge → NeoForge →
    ///     Cleanroom → LiteLoader → Fabric → Legacy Fabric → LabyMod。
    /// </summary>
    public static void MergeInstall(
        string instanceFolder,
        string instanceName,
        IReadOnlyList<InstallJsonSource> sources,
        MmcPackInfo? mmcPackInfo,
        string? labyModChannel,
        string? clientVersion = null)
    {
        var jsonPath = Path.Combine(instanceFolder, $"{instanceName}.json");
        var vanillaJson = ReadJson(jsonPath, Lang.Text("Minecraft.Version.Vanilla"));

        var arguments = new List<string>();
        var sourceByKind = sources
            .GroupBy(source => source.Kind)
            .ToDictionary(
                group => group.Key,
                group => group.Last());

        AppendArguments(vanillaJson);
        var loaded = new Dictionary<InstallJsonKind, JsonObject>();
        foreach (var kind in ArgumentOrder)
            if (sourceByKind.TryGetValue(kind, out var source))
            {
                var loaderJson = ReadJson(source.Path, GetKindName(kind));
                loaded[kind] = loaderJson;
                AppendArguments(loaderJson);
            }

        var outputJson = ApplyMmcBase(vanillaJson, mmcPackInfo);
        foreach (var kind in MergeOrder)
        {
            if (!sourceByKind.TryGetValue(kind, out var source))
                continue;
            if (IsOverridden(kind, mmcPackInfo))
            {
                LauncherCommon.Log($"[Install] 已跳过被 MMC overrides 覆盖的 {GetKindName(kind)} Json");
                continue;
            }

            if (!loaded.TryGetValue(kind, out var loaderJson))
                loaderJson = ReadJson(source.Path, GetKindName(kind));

            loaderJson.Remove("releaseTime");
            loaderJson.Remove("time");
            if (kind == InstallJsonKind.LabyMod)
                outputJson = ApplyLabyModJson(
                    outputJson,
                    loaderJson,
                    labyModChannel ?? "production");
            else
                outputJson.Merge(loaderJson);
        }

        RemoveConflictingLwjgl2(outputJson);

        if (mmcPackInfo is null || !mmcPackInfo.isMcArgsEdited)
        {
            var realArguments = string.Join(" ", arguments.Distinct());
            if (!string.IsNullOrWhiteSpace(realArguments))
                outputJson["minecraftArguments"] = realArguments;
        }
        else
        {
            outputJson.Remove("minecraftArguments");
        }

        if (sourceByKind.ContainsKey(InstallJsonKind.LabyMod))
            if (clientVersion is not null)
                outputJson["clientVersion"] = clientVersion;

        outputJson.Remove("_comment_");
        outputJson.Remove("inheritsFrom");
        outputJson.Remove("jar");
        outputJson["id"] = instanceName;

        LauncherCommon.WriteFile(jsonPath, outputJson.ToString(), new UTF8Encoding(false));
        LauncherCommon.Log($"[Install] 已合并实例 Json：{jsonPath}");
        return;

        void AppendArguments(JsonObject? json)
        {
            if (json?["minecraftArguments"] is not { } argumentsNode)
                return;
            arguments.AddRange(SplitMergeArguments(argumentsNode.ToString()));
        }
    }

    public static void Normalize(string instanceFolder, string instanceName)
    {
        MergeInstall(instanceFolder, instanceName, [], null, null);
    }

    private static JsonObject ApplyMmcBase(JsonObject vanillaJson, MmcPackInfo? mmcPackInfo)
    {
        if (mmcPackInfo is null)
            return vanillaJson;

        if (!mmcPackInfo.isMinecraftOverrided)
        {
            LauncherCommon.Log("[Install] 应用未修改原版核心的 MMC overrides");
            vanillaJson.Merge(mmcPackInfo.overridedJson);
            return vanillaJson;
        }

        LauncherCommon.Log("[Install] 原版核心已被 MMC 修改，使用 overrides 替换对应字段");
        var overriddenJson = (JsonObject)vanillaJson.DeepClone();
        foreach (var property in mmcPackInfo.overridedJson)
            overriddenJson[property.Key] = property.Value?.DeepClone();
        return overriddenJson;
    }

    private static bool IsOverridden(InstallJsonKind kind, MmcPackInfo? mmcPackInfo)
    {
        return mmcPackInfo is not null
            && kind switch
            {
                InstallJsonKind.Forge => mmcPackInfo.isForgeOverrided,
                InstallJsonKind.NeoForge => mmcPackInfo.isNeoForgeOverrided,
                InstallJsonKind.Cleanroom => mmcPackInfo.isCleanroomOverrided,
                InstallJsonKind.Fabric or InstallJsonKind.LegacyFabric => mmcPackInfo
                    .isFabricOverrided,
                _ => false
            };
    }

    private static JsonObject ApplyLabyModJson(
        JsonObject outputJson,
        JsonObject labyModJson,
        string labyModChannel)
    {
        outputJson.Merge(labyModJson);

        var labyModLib = (JsonObject)Requester.FetchJson(
            $"https://releases.r2.labymod.net/api/v1/libraries/{labyModChannel}.json",
            RequestParam.WithRetry);
        var labyModCore = (JsonObject)Requester.FetchJson(
            $"https://releases.r2.labymod.net/api/v1/manifest/{labyModChannel}/latest.json",
            RequestParam.WithRetry);
        var existingLibraries = outputJson["libraries"] as JsonArray ?? [];
        var outputLibraries = CreateLabyModLibraries(
            labyModLib,
            existingLibraries,
            labyModJson["_minecraftVersion"]);
        var commitReference = labyModCore["commitReference"]?.ToString() ?? "";
        outputLibraries.Add(CreateLabyModCoreLibrary(labyModCore, labyModChannel, commitReference));

        outputJson["libraries"] = outputLibraries;
        outputJson["labymod_data"] = new JsonObject
        {
            ["channelType"] = labyModChannel,
            ["commitReference"] = commitReference,
            ["version"] = labyModCore["labyModVersion"]?.ToString(),
            ["versionType"] = "release"
        };

        return outputJson;
    }

    private static JsonArray CreateLabyModLibraries(
        JsonObject labyModLib,
        JsonArray existingLibraries,
        JsonNode? minecraftVersion)
    {
        var outputLibraries = new JsonArray();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var isolatedLibraries = (labyModLib["isolated_libraries"]?.AsArray() ?? [])
            .Where(library =>
                (library?["versions"] as JsonArray)?.Contains(minecraftVersion) == true)
            .Select(library => library?["name"]?.ToString() ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var library in existingLibraries.OfType<JsonNode>())
        {
            var name = library["name"]?.ToString() ?? "";
            var isolatedName = name.RegexSeek(RegexPatterns.CatchLwjglInLib);
            if (isolatedName is not null && isolatedLibraries.Contains(isolatedName))
                continue;
            if (names.Add(name))
                outputLibraries.Add(library.DeepClone());
        }

        foreach (var library in (labyModLib["libraries"]?.AsArray() ?? [])
                 .OfType<JsonNode>())
        {
            var name = library["name"]?.ToString() ?? "";
            if (!names.Add(name))
                continue;
            var libraryUrl = library["url"]?.ToString() ?? "";
            outputLibraries.Add(CreateLabyModLibrary(library, libraryUrl));
        }

        return outputLibraries;
    }

    private static JsonObject CreateLabyModLibrary(JsonNode library, string libraryUrl)
    {
        const string prefix = "https://releases.r2.labymod.net/libraries/";
        var index = libraryUrl.LastIndexOfF(prefix);
        var path = index >= 0
            ? libraryUrl[(index + prefix.Length)..]
            : libraryUrl[(libraryUrl.LastIndexOfF("/") + 1)..];
        return new JsonObject
        {
            ["name"] = library["name"]?.ToString(),
            ["downloads"] = new JsonObject
            {
                ["artifact"] = new JsonObject
                {
                    ["path"] = path,
                    ["sha1"] = library["sha1"]?.ToString(),
                    ["size"] = library["size"]?.DeepClone(),
                    ["url"] = libraryUrl
                }
            }
        };
    }

    private static JsonObject CreateLabyModCoreLibrary(
        JsonObject labyModCore,
        string labyModChannel,
        string labyModCommitReference)
    {
        return new JsonObject
        {
            ["name"] = "net.labymod:LabyMod:4",
            ["downloads"] = new JsonObject
            {
                ["artifact"] = new JsonObject
                {
                    ["path"] = "net/labymod/LabyMod/4/LabyMod-4.jar",
                    ["sha1"] = labyModCore["sha1"]?.ToString(),
                    ["size"] = labyModCore["size"]?.DeepClone(),
                    ["url"] =
                        $"https://releases.r2.labymod.net/api/v1/download/labymod4/{labyModChannel}/{labyModCommitReference}.jar"
                }
            }
        };
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

    private static InstallJsonKind GuessKind(string loaderJsonPath)
    {
        var name = Path.GetFileName(loaderJsonPath).ToLowerInvariant();

        return name switch
        {
            _ when name.Contains("optifine") => InstallJsonKind.OptiFine,
            _ when name.Contains("cleanroom") => InstallJsonKind.Cleanroom,
            _ when name.Contains("neoforge") => InstallJsonKind.NeoForge,
            _ when name.Contains("forge") => InstallJsonKind.Forge,
            _ when name.Contains("liteloader") => InstallJsonKind.LiteLoader,
            _ when name.Contains("legacy-fabric") => InstallJsonKind.LegacyFabric,
            _ when name.Contains("labymod") => InstallJsonKind.LabyMod,
            _ => InstallJsonKind.Fabric
        };
    }

    private static string GetKindName(InstallJsonKind kind)
    {
        return kind switch
        {
            InstallJsonKind.OptiFine => "OptiFine",
            InstallJsonKind.Forge => "Forge",
            InstallJsonKind.NeoForge => "NeoForge",
            InstallJsonKind.Cleanroom => "Cleanroom",
            InstallJsonKind.LiteLoader => "LiteLoader",
            InstallJsonKind.Fabric => "Fabric",
            InstallJsonKind.LegacyFabric => "Legacy Fabric",
            InstallJsonKind.LabyMod => "LabyMod",
            _ => kind.ToString()
        };
    }
}