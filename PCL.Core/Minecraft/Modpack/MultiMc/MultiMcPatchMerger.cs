using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
        var mavenFiles = new List<JsonObject>();
        var gameArguments = new List<string>();
        var jvmArguments = new List<string>();
        var tweakers = new List<string>();
        var traits = new List<string>();
        var seenTraits = new HashSet<string>(StringComparer.Ordinal);
        var javaMajors = new HashSet<int>();
        var appliedUids = new List<string>(patches.Count);

        string? mainClass = null;
        JsonObject? mainJar = null;
        JsonNode? assetIndex = null;
        string? assets = null;

        foreach (var patch in patches)
        {
            var raw = patch.Raw;
            appliedUids.Add(patch.Uid);

            // 库文件：无前缀与 "+" 前缀都并入集合；同名者仅在版本更高时替换。
            libraries.AddRange(raw["libraries"] as JsonArray);
            libraries.AddRange(raw["+libraries"] as JsonArray);
            libraries.RemoveRange(raw["-libraries"] as JsonArray);
            _AppendNormalizedLibraries(mavenFiles, raw["mavenFiles"] as JsonArray);

            if (raw["mainJar"] is JsonObject patchMainJar)
                mainJar = patchMainJar.DeepClone().AsObject();

            if (raw["mainClass"]?.GetValue<string?>() is { Length: > 0 } patchMainClass)
                mainClass = patchMainClass;

            // Prism 只允许 net.minecraft 组件决定资源索引；其他组件中的同名字段不生效。
            if (string.Equals(patch.Uid, MultiMcComponentCatalog.GameUid, StringComparison.OrdinalIgnoreCase))
            {
                if (raw["assetIndex"] is JsonObject patchAssetIndex)
                    assetIndex = patchAssetIndex.DeepClone();

                if (raw["assets"]?.GetValue<string?>() is { Length: > 0 } patchAssets)
                    assets = patchAssets;
            }

            // 旧式启动参数是空格分隔的单一字符串
            if (raw["minecraftArguments"]?.GetValue<string?>() is { Length: > 0 } legacyArguments)
            {
                gameArguments.Clear();
                gameArguments.AddRange(_SplitCommandLine(legacyArguments));
            }

            _AppendStrings(gameArguments, raw["+gameArgs"] as JsonArray);
            _RemoveStrings(gameArguments, raw["-gameArgs"] as JsonArray);
            _AppendStrings(jvmArguments, raw["+jvmArgs"] as JsonArray);
            _RemoveStrings(jvmArguments, raw["-jvmArgs"] as JsonArray);
            _ApplyTweakers(tweakers, raw["+tweakers"] as JsonArray);

            foreach (var trait in _ReadStrings(raw["+traits"] as JsonArray))
            {
                if (seenTraits.Add(trait)) traits.Add(trait);
            }

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
        var localMainJarFileName = mainJar is null ? null : _ApplyMainJar(versionJson, mainJar);
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

        if (versionJson.Count == 0 && mavenFiles.Count == 0 && localMainJarFileName is null && traits.Count == 0)
            return null;

        return new ModpackVersionPatch(versionJson, ReplacesGameJson: selfContained, appliedUids)
        {
            MavenFiles = mavenFiles,
            LocalMainJarFileName = localMainJarFileName,
            Traits = traits
        };
    }

    /// <summary>
    /// 把一个原始 MultiMC 补丁应用到已有的官方版本 JSON。
    /// <para>
    /// 与 <see cref="Merge"/> 不同，本方法保留补丁在组件列表中的实际位置，并能在前置组件
    /// 已写入库列表后正确处理 <c>-libraries</c>。宿主在穿插 PCL 生成的加载器 JSON 时使用它。
    /// </para>
    /// </summary>
    public static void ApplyTo(JsonObject target, JsonObject patch)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(patch);

        // 普通字段先覆盖，随后再应用专用字段。尤其是 mainJar 必须优先于同一补丁中的
        // downloads.client，这与 Prism 的 LaunchProfile::applyMainJar 行为一致。
        _ApplyRemainingFields(target, patch);
        _ApplyLibraries(target, patch);

        if (patch["mainJar"] is JsonObject mainJar) _ApplyMainJar(target, mainJar);

        if (patch["mainClass"]?.GetValue<string?>() is { Length: > 0 } mainClass)
            target["mainClass"] = mainClass;

        if (string.Equals(
                patch["uid"]?.GetValue<string?>(),
                MultiMcComponentCatalog.GameUid,
                StringComparison.OrdinalIgnoreCase))
        {
            if (patch["assetIndex"] is JsonObject assetIndex)
                target["assetIndex"] = assetIndex.DeepClone();

            if (patch["assets"]?.GetValue<string?>() is { Length: > 0 } assets)
                target["assets"] = assets;
        }

        if (patch["compatibleJavaMajors"] is JsonArray majors)
        {
            var values = majors
                .Select(node => node?.GetValue<int?>())
                .Where(value => value is not null)
                .Select(value => value!.Value)
                .ToHashSet();
            if (_ResolveJavaVersion(values) is { } javaVersion) target["javaVersion"] = javaVersion;
        }

        _ApplyArguments(target, patch);
    }

    private static void _ApplyLibraries(JsonObject target, JsonObject patch)
    {
        if (patch["libraries"] is null && patch["+libraries"] is null && patch["-libraries"] is null)
            return;

        var libraries = new LibrarySet();
        libraries.AddRange(target["libraries"] as JsonArray);
        libraries.AddRange(patch["libraries"] as JsonArray);
        libraries.AddRange(patch["+libraries"] as JsonArray);
        libraries.RemoveRange(patch["-libraries"] as JsonArray);

        if (libraries.Count > 0) target["libraries"] = libraries.ToJsonArray();
        else target.Remove("libraries");
    }

    private static void _ApplyArguments(JsonObject target, JsonObject patch)
    {
        var hasLegacy = patch["minecraftArguments"]?.GetValue<string?>() is { Length: > 0 };
        var hasGameChanges = hasLegacy || patch["+gameArgs"] is JsonArray ||
                             patch["-gameArgs"] is JsonArray || patch["+tweakers"] is JsonArray;
        var hasJvmChanges = patch["+jvmArgs"] is JsonArray || patch["-jvmArgs"] is JsonArray;
        if (!hasGameChanges && !hasJvmChanges) return;

        var arguments = target["arguments"] as JsonObject;
        if (arguments is null)
        {
            arguments = new JsonObject();
            target["arguments"] = arguments;
        }

        if (hasGameChanges)
        {
            JsonArray game;
            if (hasLegacy)
            {
                game = _ToJsonArray(_SplitCommandLine(patch["minecraftArguments"]!.GetValue<string>()));
                arguments["game"] = game;
            }
            else
            {
                game = arguments["game"] as JsonArray ?? _MigrateLegacyGameArguments(target, arguments);
            }

            _AppendNodes(game, patch["+gameArgs"] as JsonArray);
            _RemoveNodes(game, patch["-gameArgs"] as JsonArray);

            _ApplyTweakerArguments(game, patch["+tweakers"] as JsonArray);

            // 同一份版本 JSON 不能同时保留旧、新两套游戏参数，否则 PCL 会重复传入。
            target.Remove("minecraftArguments");
        }

        if (hasJvmChanges)
        {
            var jvm = _GetOrCreateArray(arguments, "jvm");
            _AppendNodes(jvm, patch["+jvmArgs"] as JsonArray);
            _RemoveNodes(jvm, patch["-jvmArgs"] as JsonArray);
        }
    }

    /// <summary>
    /// 保留格式未来扩展的标量与普通数组字段；组件元数据和已翻译字段不写入官方版本 JSON。
    /// </summary>
    private static void _ApplyRemainingFields(JsonObject target, JsonObject patch)
    {
        foreach (var (key, value) in patch)
        {
            if (_HandledOrMetadataFields.Contains(key)) continue;

            if (key.StartsWith('+') && key.Length > 1 && value is JsonArray additions)
            {
                _AppendNodes(_GetOrCreateArray(target, key[1..]), additions);
                continue;
            }

            if (key.StartsWith('-') && key.Length > 1 && value is JsonArray removals)
            {
                if (target[key[1..]] is JsonArray existing) _RemoveNodes(existing, removals);
                continue;
            }

            target[key] = value?.DeepClone();
        }
    }

    private static JsonArray _GetOrCreateArray(JsonObject owner, string propertyName)
    {
        if (owner[propertyName] is JsonArray existing) return existing;

        var created = new JsonArray();
        owner[propertyName] = created;
        return created;
    }

    private static JsonArray _MigrateLegacyGameArguments(JsonObject target, JsonObject arguments)
    {
        var game = target["minecraftArguments"]?.GetValue<string?>() is { Length: > 0 } legacy
            ? _ToJsonArray(_SplitCommandLine(legacy))
            : new JsonArray();
        arguments["game"] = game;
        return game;
    }

    private static void _AppendNodes(JsonArray target, JsonArray? source)
    {
        if (source is null) return;
        foreach (var node in source) target.Add(node?.DeepClone());
    }

    private static void _RemoveNodes(JsonArray target, JsonArray? removals)
    {
        if (removals is null) return;

        foreach (var removal in removals)
        {
            for (var index = target.Count - 1; index >= 0; index--)
            {
                if (JsonNode.DeepEquals(target[index], removal)) target.RemoveAt(index);
            }
        }
    }

    private static readonly HashSet<string> _HandledOrMetadataFields = new(StringComparer.Ordinal)
    {
        "formatVersion", "uid", "id", "name", "version", "order", "requires", "conflicts", "volatile",
        "type", "releaseTime",
        "libraries", "+libraries", "-libraries", "mavenFiles", "jarMods", "+jarMods",
        "mainJar", "mainClass", "assetIndex", "assets", "compatibleJavaMajors", "minecraftArguments",
        "+gameArgs", "-gameArgs", "+jvmArgs", "-jvmArgs", "+tweakers", "+traits"
    };

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

    private static void _ApplyTweakers(List<string> target, JsonArray? source)
    {
        var additions = _ReadStrings(source).ToArray();
        if (additions.Length == 0) return;

        var moved = additions.ToHashSet(StringComparer.Ordinal);
        target.RemoveAll(moved.Contains);
        target.AddRange(additions);
    }

    private static void _ApplyTweakerArguments(JsonArray target, JsonArray? source)
    {
        var additions = _ReadStrings(source).ToArray();
        if (additions.Length == 0) return;

        var moved = additions.ToHashSet(StringComparer.Ordinal);
        for (var index = target.Count - 2; index >= 0; index--)
        {
            if (target[index] is not JsonValue flag ||
                !flag.TryGetValue<string>(out var flagValue) ||
                flagValue != "--tweakClass" ||
                target[index + 1] is not JsonValue value ||
                !value.TryGetValue<string>(out var tweaker) ||
                !moved.Contains(tweaker))
                continue;

            target.RemoveAt(index + 1);
            target.RemoveAt(index);
        }

        foreach (var tweaker in additions)
        {
            target.Add("--tweakClass");
            target.Add(tweaker);
        }
    }

    private static void _RemoveStrings(List<string> target, JsonArray? source)
    {
        if (source is null) return;
        foreach (var value in _ReadStrings(source)) target.RemoveAll(item => item == value);
    }

    private static IEnumerable<string> _ReadStrings(JsonArray? source)
        => source is null
            ? []
            : source.Select(item => item?.GetValue<string?>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!);

    /// <summary>
    /// 按 MultiMC 的命令行规则切分旧式 <c>minecraftArguments</c>，保留引号中的空格。
    /// </summary>
    private static IReadOnlyList<string> _SplitCommandLine(string arguments)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var escaped = false;
        var quote = '\0';

        foreach (var character in arguments)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
            }
            else if (quote != '\0')
            {
                if (character == '\\') escaped = true;
                else if (character == quote) quote = '\0';
                else current.Append(character);
            }
            else if (char.IsWhiteSpace(character))
            {
                if (current.Length == 0) continue;
                result.Add(current.ToString());
                current.Clear();
            }
            else if (character is '\"' or '\'')
            {
                quote = character;
            }
            else
            {
                current.Append(character);
            }
        }

        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static JsonArray _ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values) array.Add(value);
        return array;
    }

    /// <summary>
    /// 按 Maven 坐标去重的库文件集合。
    /// <para>
    /// Prism 的 <c>LaunchProfile::applyLibrary</c> 以 <c>group:artifact[:classifier]</c>
    /// 作为标识，且只有新条目的版本严格更高时才替换旧条目。
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
                // 非当前平台的条目仍保留在版本 JSON 中，但不得参与当前平台的版本去重。
                // 否则一个仅限 Linux/macOS 的高版本会替换 Windows 正在使用的低版本。
                if (!IsLibraryActiveOnCurrentSystem(library))
                {
                    _items.Add(library);
                    continue;
                }

                var key = _GetIdentity(library);

                if (key is not null && _indexByKey.TryGetValue(key, out var existing))
                {
                    var existingVersion = _GetVersion(_items[existing]);
                    var candidateVersion = _GetVersion(library);
                    if (_CompareLauncherVersions(candidateVersion, existingVersion) > 0)
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
                var key = node is JsonObject obj ? _GetIdentity(obj) : null;
                var coordinateKey = node switch
                {
                    JsonObject => null,
                    JsonValue value when value.TryGetValue<string>(out var name) => _GetIdentityFromName(name),
                    _ => null
                };

                if (key is null && coordinateKey is null) continue;

                for (var index = 0; index < _items.Count; index++)
                {
                    if (_items[index] is not { } item) continue;

                    var itemKey = _GetIdentity(item);
                    var matches = key is not null
                        ? string.Equals(itemKey, key, StringComparison.OrdinalIgnoreCase)
                        : string.Equals(
                            _GetIdentityFromName(item["name"]?.GetValue<string?>()),
                            coordinateKey,
                            StringComparison.OrdinalIgnoreCase);
                    if (!matches) continue;

                    if (itemKey is not null) _indexByKey.Remove(itemKey);
                    _items[index] = null;
                }
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
            => NormalizeLibrary(source);

        private static string? _GetIdentity(JsonObject library)
        {
            var coordinate = _GetIdentityFromName(library["name"]?.GetValue<string?>());
            if (coordinate is null) return null;

            var kind = library["natives"] is JsonObject { Count: > 0 } ? "native" : "library";
            return $"{kind}:{coordinate}";
        }

        private static string _GetVersion(JsonObject? library)
            => _TryParseMavenCoordinate(library?["name"]?.GetValue<string?>(), out var coordinate)
                ? coordinate.Version
                : string.Empty;

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

    /// <summary>将 MultiMC 专有库字段翻译为 PCL 可消费的官方版本 JSON 字段。</summary>
    public static JsonObject NormalizeLibrary(JsonObject source)
    {
        var library = source.DeepClone().AsObject();

        var name = _GetOptionalString(library, "name");
        if (!_TryParseMavenCoordinate(name, out _))
            throw new FormatException($"MultiMC 库坐标不合法：{name}");

        _ValidateOptionalString(library, "MMC-hint");
        _ValidateOptionalString(library, "MMC-displayname");
        _ValidateOptionalString(library, "MMC-filename");
        _ValidateOptionalString(library, "MMC-absoluteUrl");
        _ValidateOptionalString(library, "MMC-absulute_url");
        _ValidateOptionalString(library, "url");
        _ValidateOptionalString(library, "hint");
        _ValidateOptionalString(library, "displayName");

        if (library.ContainsKey("natives") && library["natives"] is not JsonObject)
            throw new FormatException($"MultiMC 库 {name} 的 natives 必须是对象");
        if (library["natives"] is JsonObject natives)
            foreach (var native in natives)
                if (native.Value is not JsonValue value || !value.TryGetValue<string>(out _))
                    throw new FormatException(
                        $"MultiMC 库 {name} 的 native classifier {native.Key} 必须是字符串");

        if (library.Remove("MMC-hint", out var hint)) library["hint"] = hint;
        if (library.Remove("MMC-displayname", out var displayName)) library["displayName"] = displayName;

        JsonNode? absoluteUrl = null;
        if (!library.Remove("MMC-absoluteUrl", out absoluteUrl))
            library.Remove("MMC-absulute_url", out absoluteUrl);

        var customFileName = _GetOptionalString(library, "MMC-filename");
        library.Remove("MMC-filename");

        var defaultArtifactPath = _GetMavenPath(name, customFileName?.Trim());
        if (library.ContainsKey("downloads") && library["downloads"] is not JsonObject)
            throw new FormatException($"MultiMC 库 {name} 的 downloads 必须是对象");
        var downloads = library["downloads"] as JsonObject ?? new JsonObject();
        library["downloads"] = downloads;

        if (downloads.ContainsKey("artifact") && downloads["artifact"] is not JsonObject)
            throw new FormatException($"MultiMC 库 {name} 的 downloads.artifact 必须是对象");
        if (downloads["artifact"] is not JsonObject artifact)
        {
            artifact = new JsonObject();
            downloads["artifact"] = artifact;
        }
        _ValidateOptionalString(artifact, "path", $"MultiMC 库 {name} 的 downloads.artifact.path");
        _ValidateOptionalString(artifact, "url", $"MultiMC 库 {name} 的 downloads.artifact.url");
        _ValidateOptionalString(artifact, "sha1", $"MultiMC 库 {name} 的 downloads.artifact.sha1");
        if (!string.IsNullOrWhiteSpace(customFileName) || artifact["path"] is null)
            artifact["path"] = defaultArtifactPath;

        // MMC-filename 会覆盖所有制品的落盘文件名，包括 native classifier。
        if (downloads.ContainsKey("classifiers") && downloads["classifiers"] is not JsonObject)
            throw new FormatException($"MultiMC 库 {name} 的 downloads.classifiers 必须是对象");
        if (downloads["classifiers"] is JsonObject classifiers)
        {
            foreach (var (classifierName, value) in classifiers)
            {
                if (value is not JsonObject classifier)
                    throw new FormatException(
                        $"MultiMC 库 {name} 的 classifier {classifierName} 必须是对象");
                _ValidateOptionalString(classifier, "path",
                    $"MultiMC 库 {name} 的 classifier {classifierName}.path");
                _ValidateOptionalString(classifier, "url",
                    $"MultiMC 库 {name} 的 classifier {classifierName}.url");
                _ValidateOptionalString(classifier, "sha1",
                    $"MultiMC 库 {name} 的 classifier {classifierName}.sha1");
                if (!string.IsNullOrWhiteSpace(customFileName)) classifier["path"] = defaultArtifactPath;
            }
        }

        if (absoluteUrl?.GetValue<string?>() is { Length: > 0 } url)
        {
            artifact["url"] = url;
        }

        return library;
    }

    /// <summary>返回 MultiMC 库最终使用的文件名。</summary>
    public static string? GetLibraryFileName(JsonObject library)
    {
        if (library["MMC-filename"]?.GetValue<string?>() is { Length: > 0 } custom)
            return custom.Trim();

        var artifactPath = library["downloads"]?["artifact"]?["path"]?.GetValue<string?>();
        if (!string.IsNullOrWhiteSpace(artifactPath))
            return artifactPath.Replace('\\', '/').Split('/')[^1];

        return _TryParseMavenCoordinate(library["name"]?.GetValue<string?>(), out var coordinate)
            ? coordinate.FileName
            : null;
    }

    /// <summary>返回库制品在 Maven 仓库中的相对路径。</summary>
    public static string GetLibraryArtifactPath(JsonObject library)
    {
        var normalized = NormalizeLibrary(library);
        return normalized["downloads"]!["artifact"]!["path"]!.GetValue<string>();
    }

    private static void _AppendNormalizedLibraries(List<JsonObject> target, JsonArray? source)
    {
        if (source is null) return;
        target.AddRange(source.OfType<JsonObject>().Select(NormalizeLibrary));
    }

    /// <returns>本地主 JAR 的文件名；远程主 JAR 返回 <c>null</c>。</returns>
    private static string? _ApplyMainJar(JsonObject target, JsonObject source)
    {
        var library = NormalizeLibrary(source);
        var isLocal = string.Equals(
            library["hint"]?.GetValue<string?>(), "local", StringComparison.OrdinalIgnoreCase);

        var downloads = target["downloads"] as JsonObject;
        if (isLocal)
        {
            downloads?.Remove("client");
            if (downloads is { Count: 0 }) target.Remove("downloads");
            return GetLibraryFileName(source);
        }

        var artifact = library["downloads"]?["artifact"] as JsonObject;
        var client = artifact?.DeepClone().AsObject() ?? new JsonObject();

        if (client["url"] is null && library["url"]?.GetValue<string?>() is { Length: > 0 } repository)
        {
            var path = client["path"]?.GetValue<string?>() ??
                       _GetMavenPath(library["name"]?.GetValue<string?>(), null);
            client["url"] = $"{repository.TrimEnd('/')}/{path}";
        }

        if (client["url"] is null)
        {
            var path = client["path"]?.GetValue<string?>() ??
                       _GetMavenPath(library["name"]?.GetValue<string?>(), null);
            client["url"] = $"https://libraries.minecraft.net/{path.Replace('\\', '/')}";
        }

        downloads ??= new JsonObject();
        target["downloads"] = downloads;
        downloads["client"] = client;
        return null;
    }

    private static string _GetMavenPath(string? name, string? customFileName)
    {
        if (!_TryParseMavenCoordinate(name, out var coordinate))
            throw new FormatException($"MultiMC 库坐标不合法：{name}");

        var fileName = string.IsNullOrWhiteSpace(customFileName) ? coordinate.FileName : customFileName;
        if (fileName.Contains('/') || fileName.Contains('\\') || fileName is "." or "..")
            throw new FormatException($"MultiMC 库文件名不合法：{fileName}");

        return $"{coordinate.Group.Replace('.', '/')}/{coordinate.Artifact}/{coordinate.Version}/{fileName}";
    }

    private static bool _TryParseMavenCoordinate(string? name, out MavenCoordinate coordinate)
    {
        coordinate = default;
        if (string.IsNullOrWhiteSpace(name)) return false;

        var coordinateAndExtension = name.Split('@', 2);
        var parts = coordinateAndExtension[0].Split(':');
        if (parts.Length is < 3 or > 4 || parts.Any(string.IsNullOrWhiteSpace)) return false;
        if (parts.Any(part => part.Contains('/') || part.Contains('\\') || part is "." or "..")) return false;
        if (parts[0].Split('.').Any(string.IsNullOrWhiteSpace)) return false;

        var extension = coordinateAndExtension.Length == 2 && coordinateAndExtension[1].Length > 0
            ? coordinateAndExtension[1]
            : "jar";
        if (extension.Contains('/') || extension.Contains('\\') || extension is "." or "..") return false;

        var classifier = parts.Length == 4 ? parts[3] : null;
        var fileName = $"{parts[1]}-{parts[2]}{(classifier is null ? "" : $"-{classifier}")}.{extension}";
        coordinate = new MavenCoordinate(parts[0], parts[1], parts[2], fileName);
        return true;
    }

    private readonly record struct MavenCoordinate(
        string Group,
        string Artifact,
        string Version,
        string FileName);

    /// <summary>
    /// 与 Prism Launcher 的 <c>Version</c> 比较保持一致：按点分段，先比较每段的
    /// 数字前缀，再按序比较剩余文本，缺失段视为 <c>0</c>。
    /// </summary>
    private static int _CompareLauncherVersions(string left, string right)
    {
        var leftSections = left.Split('.');
        var rightSections = right.Split('.');
        var count = Math.Max(leftSections.Length, rightSections.Length);

        for (var index = 0; index < count; index++)
        {
            var leftSection = _ParseVersionSection(index < leftSections.Length ? leftSections[index] : "0");
            var rightSection = _ParseVersionSection(index < rightSections.Length ? rightSections[index] : "0");

            int comparison;
            if (leftSection.HasNumber && rightSection.HasNumber)
            {
                comparison = leftSection.Number.CompareTo(rightSection.Number);
                if (comparison == 0)
                    comparison = string.CompareOrdinal(leftSection.Suffix, rightSection.Suffix);
            }
            else
            {
                comparison = string.CompareOrdinal(leftSection.Full, rightSection.Full);
            }

            if (comparison != 0) return comparison;
        }

        return 0;
    }

    private static VersionSection _ParseVersionSection(string value)
    {
        var digitCount = 0;
        while (digitCount < value.Length && char.IsDigit(value[digitCount])) digitCount++;

        if (digitCount == 0)
            return new VersionSection(false, 0, string.Empty, value);

        var number = int.TryParse(value.AsSpan(0, digitCount), out var parsed) ? parsed : 0;
        return new VersionSection(true, number, value[digitCount..], value);
    }

    /// <summary>
    /// 判断库在 PCL 当前支持的平台（Windows）是否生效。规则顺序、默认拒绝以及 native
    /// classifier 的处理与 Prism/PCL 的运行时库解析保持一致。
    /// </summary>
    public static bool IsLibraryActiveOnCurrentSystem(JsonObject library)
    {
        if (library["natives"] is JsonObject { Count: > 0 } natives)
        {
            if (natives["windows"] is not JsonValue windowsClassifier) return false;
            if (!windowsClassifier.TryGetValue<string>(out _))
                throw new FormatException("MultiMC 库的 natives.windows 必须是字符串");
        }

        if (!library.ContainsKey("rules")) return true;
        if (library["rules"] is not JsonArray rules)
            throw new FormatException("MultiMC 库的 rules 必须是数组");

        var active = false;
        foreach (var node in rules)
        {
            if (node is not JsonObject rule)
                throw new FormatException("MultiMC 库的 rules 中包含非对象条目");

            var action = _GetOptionalString(rule, "action") ??
                         throw new FormatException("MultiMC 库规则缺少 action");
            if (action is not ("allow" or "disallow"))
                throw new FormatException($"MultiMC 库规则包含未知 action：{action}");
            if (!_RuleMatchesCurrentSystem(rule)) continue;

            active = action == "allow";
        }

        return active;
    }

    private static bool _RuleMatchesCurrentSystem(JsonObject rule)
    {
        if (rule.ContainsKey("os") && rule["os"] is not JsonObject)
            throw new FormatException("MultiMC 库规则的 os 必须是对象");
        if (rule["os"] is JsonObject os)
        {
            var name = _GetOptionalString(os, "name");
            if (name is not null && name is not ("windows" or "unknown")) return false;

            if (_GetOptionalString(os, "version") is { Length: > 0 } pattern)
            {
                try
                {
                    if (!Regex.IsMatch(Environment.OSVersion.Version.ToString(), pattern)) return false;
                }
                catch (ArgumentException ex)
                {
                    throw new FormatException($"MultiMC 库规则包含非法的系统版本表达式：{pattern}", ex);
                }
            }

            if (_GetOptionalString(os, "arch") is { Length: > 0 } architecture &&
                (architecture == "x86") != !Environment.Is64BitOperatingSystem)
                return false;
        }

        if (rule.ContainsKey("features") && rule["features"] is not JsonObject)
            throw new FormatException("MultiMC 库规则的 features 必须是对象");
        if (rule["features"] is JsonObject features)
        {
            // PCL 不以 Demo 身份启动，也不主动启用 Quick Play 特征。
            if (features.ContainsKey("is_demo_user")) return false;
            if (features.Any(feature => feature.Key.Contains("quick_play", StringComparison.Ordinal)))
                return false;
        }

        return true;
    }

    private static string? _GetOptionalString(JsonObject owner, string propertyName)
    {
        if (!owner.TryGetPropertyValue(propertyName, out var node) || node is null) return null;
        if (node is JsonValue value && value.TryGetValue<string>(out var result)) return result;
        throw new FormatException($"{propertyName} 必须是字符串");
    }

    private static void _ValidateOptionalString(
        JsonObject owner, string propertyName, string? description = null)
    {
        try
        {
            _GetOptionalString(owner, propertyName);
        }
        catch (FormatException ex)
        {
            throw new FormatException(description ?? ex.Message, ex);
        }
    }

    private readonly record struct VersionSection(bool HasNumber, int Number, string Suffix, string Full);
}
