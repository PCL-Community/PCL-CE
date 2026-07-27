using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack.MultiMc;

/// <summary>
/// 把若干 MultiMC JSON Patch 合并成一份官方格式的版本 JSON 片段。
/// <para>
/// 本类只负责「怎么合并」这一机制，不决定「合并哪些补丁」——
/// 后者是安装策略，由 <see cref="Providers.MultiMcModpackProvider"/> 决定。
/// </para>
/// <para>
/// 字段前缀规则取自 MultiMC 的 JSON Patches 文档：<c>+</c> 表示追加，
/// <c>-</c> 表示移除，无前缀表示覆盖。
/// </para>
/// </summary>
public static class MultiMcPatchMerger
{
    /// <summary>
    /// 合并补丁列表。
    /// </summary>
    /// <param name="patches">按应用顺序排列的补丁。</param>
    /// <param name="selfContained">
    /// 为 <c>true</c> 时产出一份可独立使用的完整版本 JSON（补齐官方 JVM 参数）；
    /// 为 <c>false</c> 时只产出增量，供叠加到启动器生成的版本 JSON 之上。
    /// </param>
    /// <returns>补丁列表为空或无有效内容时返回 <c>null</c>。</returns>
    public static ModpackVersionPatch? Merge(IReadOnlyList<MultiMcPatch> patches, bool selfContained)
    {
        if (patches.Count == 0) return null;

        var libraries = new LibrarySet();
        var gameArguments = new List<string>();
        var jvmArguments = new List<string>();
        var tweakers = new List<string>();
        var traits = new HashSet<string>(StringComparer.Ordinal);
        var javaMajors = new HashSet<int>();
        var appliedUids = new List<string>(patches.Count);

        string? mainClass = null;
        JsonNode? assetIndex = null;
        string? assets = null;

        foreach (var patch in patches)
        {
            var raw = patch.Raw;
            appliedUids.Add(patch.Uid);

            // 库文件：无前缀与 "+" 前缀都并入集合，同名者以后者为准；"-" 前缀按名称移除
            libraries.AddRange(raw["libraries"] as JsonArray);
            libraries.AddRange(raw["+libraries"] as JsonArray);
            libraries.AddRange(raw["mavenFiles"] as JsonArray);
            libraries.RemoveRange(raw["-libraries"] as JsonArray);

            if (raw["mainClass"]?.GetValue<string?>() is { Length: > 0 } patchMainClass)
                mainClass = patchMainClass;

            if (raw["assetIndex"] is JsonObject patchAssetIndex)
                assetIndex = patchAssetIndex.DeepClone();

            if (raw["assets"]?.GetValue<string?>() is { Length: > 0 } patchAssets)
                assets = patchAssets;

            // 旧式启动参数是空格分隔的单一字符串
            if (raw["minecraftArguments"]?.GetValue<string?>() is { Length: > 0 } legacyArguments)
            {
                gameArguments.Clear();
                gameArguments.AddRange(
                    legacyArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            _AppendStrings(gameArguments, raw["+gameArgs"] as JsonArray);
            _AppendStrings(jvmArguments, raw["+jvmArgs"] as JsonArray);
            _AppendStrings(tweakers, raw["+tweakers"] as JsonArray);

            foreach (var trait in _ReadStrings(raw["+traits"] as JsonArray)) traits.Add(trait);

            if (raw["compatibleJavaMajors"] is JsonArray majors)
                foreach (var major in majors)
                {
                    if (major?.GetValue<int?>() is { } value) javaMajors.Add(value);
                }
        }

        // LaunchWrapper 的 tweakClass 以游戏参数的形式传入
        foreach (var tweaker in tweakers)
        {
            gameArguments.Add("--tweakClass");
            gameArguments.Add(tweaker);
        }

        var versionJson = new JsonObject();

        if (libraries.Count > 0) versionJson["libraries"] = libraries.ToJsonArray();
        if (mainClass is not null) versionJson["mainClass"] = mainClass;
        if (assetIndex is not null) versionJson["assetIndex"] = assetIndex;
        if (assets is not null) versionJson["assets"] = assets;

        if (_ResolveJavaVersion(javaMajors) is { } javaVersion) versionJson["javaVersion"] = javaVersion;

        // 自包含模式下必须补齐官方 JSON 里的固定 JVM 参数，否则实例缺少 classpath 与 natives 路径；
        // 增量模式下这些参数已存在于被叠加的 JSON 中，重复写入会让命令行出现两份 -cp。
        if (selfContained && (jvmArguments.Count > 0 || gameArguments.Count > 0))
            jvmArguments.InsertRange(0, _StandardJvmArguments);

        if (gameArguments.Count > 0 || jvmArguments.Count > 0)
        {
            var arguments = new JsonObject();
            if (gameArguments.Count > 0) arguments["game"] = _ToJsonArray(gameArguments);
            if (jvmArguments.Count > 0) arguments["jvm"] = _ToJsonArray(jvmArguments);
            versionJson["arguments"] = arguments;
        }

        if (versionJson.Count == 0) return null;

        return new ModpackVersionPatch(versionJson, ReplacesGameJson: selfContained, appliedUids);
    }

    /// <summary>
    /// 官方版本 JSON 中固定存在的 JVM 参数。
    /// </summary>
    private static readonly string[] _StandardJvmArguments =
    [
        "-Djava.library.path=${natives_directory}",
        "-Dminecraft.launcher.brand=${launcher_name}",
        "-Dminecraft.launcher.version=${launcher_version}",
        "-cp",
        "${classpath}"
    ];

    /// <summary>
    /// 把 <c>compatibleJavaMajors</c> 映射为官方 JSON 的 <c>javaVersion</c> 段。
    /// 列表中取启动器已知的最高版本，未知时只写主版本号。
    /// </summary>
    private static JsonObject? _ResolveJavaVersion(HashSet<int> javaMajors)
    {
        if (javaMajors.Count == 0) return null;

        var preferred = javaMajors.Where(_KnownJavaComponents.ContainsKey).DefaultIfEmpty(0).Max();
        if (preferred == 0) preferred = javaMajors.Max();

        var result = new JsonObject { ["majorVersion"] = preferred };
        if (_KnownJavaComponents.TryGetValue(preferred, out var component)) result["component"] = component;
        return result;
    }

    /// <summary>Mojang 官方 Java 运行时组件名。</summary>
    private static readonly Dictionary<int, string> _KnownJavaComponents = new()
    {
        [8] = "jre-legacy",
        [16] = "java-runtime-alpha",
        [17] = "java-runtime-gamma",
        [21] = "java-runtime-delta"
    };

    private static void _AppendStrings(List<string> target, JsonArray? source)
        => target.AddRange(_ReadStrings(source));

    private static IEnumerable<string> _ReadStrings(JsonArray? source)
        => source is null
            ? []
            : source.Select(item => item?.GetValue<string?>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!);

    private static JsonArray _ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values) array.Add(value);
        return array;
    }

    /// <summary>
    /// 按 Maven 坐标去重的库文件集合。
    /// <para>
    /// MultiMC 中后应用的组件可以替换先前同名的库（典型场景是加载器替换原版的 ASM），
    /// 因此以 <c>group:artifact[:classifier]</c> 作为标识，保留插入顺序。
    /// </para>
    /// </summary>
    private sealed class LibrarySet
    {
        private readonly Dictionary<string, int> _indexByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<JsonObject?> _items = [];

        public int Count => _items.Count(item => item is not null);

        public void AddRange(JsonArray? source)
        {
            if (source is null) return;

            foreach (var node in source.OfType<JsonObject>())
            {
                var library = _NormalizeLibrary(node);
                var key = _GetIdentity(library);

                if (key is not null && _indexByKey.TryGetValue(key, out var existing))
                {
                    _items[existing] = library;
                    continue;
                }

                if (key is not null) _indexByKey[key] = _items.Count;
                _items.Add(library);
            }
        }

        public void RemoveRange(JsonArray? source)
        {
            if (source is null) return;

            foreach (var node in source)
            {
                // "-libraries" 的条目可能是完整对象，也可能只是坐标字符串
                var key = node switch
                {
                    JsonObject obj => _GetIdentity(obj),
                    JsonValue value when value.TryGetValue<string>(out var name) => _GetIdentityFromName(name),
                    _ => null
                };

                if (key is not null && _indexByKey.Remove(key, out var index)) _items[index] = null;
            }
        }

        public JsonArray ToJsonArray()
        {
            var array = new JsonArray();
            foreach (var item in _items)
            {
                if (item is not null) array.Add(item);
            }
            return array;
        }

        /// <summary>
        /// 将 MultiMC 专有字段翻译为官方 JSON 字段。
        /// </summary>
        private static JsonObject _NormalizeLibrary(JsonObject source)
        {
            var library = source.DeepClone().AsObject();

            // MMC-hint 描述该库的获取方式（local 表示由整合包自带），改用官方 JSON 不冲突的键名保留
            if (library.Remove("MMC-hint", out var hint)) library["hint"] = hint;

            // MMC-absoluteUrl 是精确下载地址，翻译为 downloads.artifact.url
            if (library.Remove("MMC-absoluteUrl", out var absoluteUrl)
                && absoluteUrl?.GetValue<string?>() is { Length: > 0 } url)
            {
                var downloads = library["downloads"] as JsonObject;
                if (downloads is null)
                {
                    downloads = new JsonObject();
                    library["downloads"] = downloads;
                }

                var artifact = downloads["artifact"] as JsonObject;
                if (artifact is null)
                {
                    artifact = new JsonObject();
                    downloads["artifact"] = artifact;
                }

                artifact["url"] = url;
            }

            return library;
        }

        private static string? _GetIdentity(JsonObject library)
            => _GetIdentityFromName(library["name"]?.GetValue<string?>());

        /// <summary>
        /// 由 Maven 坐标 <c>group:artifact:version[:classifier][@ext]</c> 得到去重标识。
        /// 版本号不参与标识，使不同版本的同一库互相替换。
        /// </summary>
        private static string? _GetIdentityFromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var coordinate = name.Split('@', 2)[0];
            var parts = coordinate.Split(':');
            if (parts.Length < 2) return null;

            return parts.Length >= 4
                ? $"{parts[0]}:{parts[1]}:{parts[3]}"
                : $"{parts[0]}:{parts[1]}";
        }
    }
}
