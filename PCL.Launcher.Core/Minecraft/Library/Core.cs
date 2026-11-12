using PCL.Core.Utils.OS;

namespace PCL;

public static partial class Libraries
{
    private static readonly string OsVersion = Environment.OSVersion.Version.ToString();

    /// <summary>
    ///     检查是否符合 JSON 中的 Rules。
    /// </summary>
    /// <param name="ruleToken">JSON 中的 "rules" 项目。</param>
    public static bool McJsonRuleCheck(JsonNode? ruleToken)
    {
        if (ruleToken is null)
            return true;

        var required = false;
        foreach (var rule in ruleToken.AsArray() ?? [])
        {
            if (rule is null)
                continue;

            var osMatch = _IsRuleOsMatch(rule["os"]);
            var featuresMatch = _IsRuleFeaturesMatch(rule["features"]);
            var isRightRule = osMatch && featuresMatch;
            if (rule["action"]?.ToString() == "allow")
            {
                if (isRightRule)
                    required = true;
            }
            else if (isRightRule)
            {
                required = false;
            }
        }

        return required;
    }

    private static bool _IsRuleOsMatch(JsonNode? os)
    {
        if (os is null)
            return true;

        var result = true;
        var osNameNode = os["name"];
        if (osNameNode is not null)
        {
            var osName = osNameNode.ToString();
            switch (osName)
            {
                case "unknown":
                    break;
                case "windows":
                    var osVersionNode = os["version"];
                    if (osVersionNode is not null)
                        result = OsVersion.RegexCheck(osVersionNode.ToString());
                    break;
                default:
                    result = false;
                    break;
            }
        }

        var archNode = os["arch"];
        if (archNode is not null)
            result = result && archNode.ToString() == "x86" == SystemInfo.Is32BitSystem;
        return result;
    }

    private static bool _IsRuleFeaturesMatch(JsonNode? features)
    {
        if (features is null)
            return true;

        var result = features["is_demo_user"] is null;
        if (features.AsObject()?.Any(prop => prop.Key.Contains("quick_play")) == true)
            result = false;
        return result;
    }

    /// <summary>
    ///     递归获取 Minecraft 某一实例的完整支持库列表。
    /// </summary>
    public static List<McLibToken> McLibListGet(McInstance mcInstance, bool includeInstanceJar)
    {
        LauncherCommon.Log($"[Minecraft] 获取支持库列表：{mcInstance.Name}");
        var result = McLibListGetWithJson(mcInstance.JsonObject, targetMcInstance: mcInstance);

        if (includeInstanceJar)
            _AddClientJarToken(result, _ResolveMcLibRealInstance(mcInstance));

        return result;
    }

    private static McInstance _ResolveMcLibRealInstance(McInstance mcInstance)
    {
        McInstance realMcInstance;
        var requiredJar = mcInstance.JsonObject["jar"]?.ToString();
        if (mcInstance.IsHmclFormatJson || requiredJar is null)
        {
            var originalInstance = mcInstance;
            if (!(
                    (mcInstance.Info.HasForge || mcInstance.Info.HasNeoForge)
                    && mcInstance.Info.Drop >= 170))
                while (!string.IsNullOrEmpty(originalInstance.InheritInstanceName))
                {
                    if (originalInstance.InheritInstanceName == originalInstance.Name)
                        break;
                    originalInstance = new McInstance(
                        Path.Combine(
                            GameFolders.mcFolderSelected ?? "",
                            "versions",
                            originalInstance.InheritInstanceName));
                }

            realMcInstance = new McInstance(originalInstance.PathInstance);
        }
        else
        {
            realMcInstance = new McInstance(requiredJar);
        }

        if (File.Exists($"{realMcInstance.PathInstance}{realMcInstance.Name}.json"))
            return realMcInstance;

        realMcInstance = mcInstance;
        LauncherCommon.Log(
            $"[Minecraft] 可能缺少前置实例 {realMcInstance.Name}，找不到对应的 JSON 文件",
            LauncherLogLevel.Debug);
        return realMcInstance;
    }

    private static void _AddClientJarToken(List<McLibToken> result, McInstance realMcInstance)
    {
        var clientDownload = realMcInstance.JsonObject["downloads"]?["client"];
        result.Add(
            new McLibToken
            {
                LocalPath = $"{realMcInstance.PathInstance}{realMcInstance.Name}.jar",
                size = 0L,
                IsNatives = false,
                Url = (string?)clientDownload?["url"],
                Sha1 = (string?)clientDownload?["sha1"]
            });
    }

    /// <summary>
    ///     获取 Minecraft 某一实例忽视继承的支持库列表，即结果中没有继承项。
    /// </summary>
    public static List<McLibToken> McLibListGetWithJson(
        JsonObject jsonObject,
        bool keepSameNameDifferentVersionResult = false,
        string? customMcFolder = null,
        McInstance? targetMcInstance = null)
    {
        customMcFolder ??= GameFolders.mcFolderSelected ?? "";
        var basicArray = new List<McLibToken>();

        var allLibs = jsonObject["libraries"] as JsonArray ?? [];

        foreach (var libraryNode in allLibs)
        {
            if (libraryNode is not JsonObject library)
                continue;
            _RemoveNullLibraryValues(library);
            if (!McJsonRuleCheck(library["rules"]))
                continue;

            var name = library["name"]?.ToString() ?? "";
            var rootUrl = library["url"]?.ToString();
            if (rootUrl is not null)
                rootUrl += McLibGet(
                        name,
                        false,
                        true,
                        customMcFolder)
                    .Replace(@"\", "/");

            var hint = library["hint"]?.ToString();
            var isLocal = hint is not null && hint == "local";

            if (library["natives"] is null)
            {
                var token = _BuildMcLibToken(
                    library,
                    name,
                    rootUrl,
                    isLocal,
                    customMcFolder,
                    targetMcInstance);
                if (token is not null)
                    basicArray.Add(token);
            }
            else if (library["natives"]?["windows"] is { } windows)
            {
                var token = _BuildMcLibNativeToken(
                    library,
                    name,
                    rootUrl,
                    isLocal,
                    customMcFolder,
                    windows.ToString());
                if (token is not null)
                    basicArray.Add(token);
            }
        }

        return _DeduplicateMcLibTokens(basicArray, keepSameNameDifferentVersionResult);
    }

    private static void _RemoveNullLibraryValues(JsonObject library)
    {
        var keysToRemove = library
            .Where(p => p.Value?.GetValueKind() == JsonValueKind.Null)
            .Select(p => p.Key)
            .ToList();
        foreach (var key in keysToRemove)
            library.Remove(key);
    }

    private static McLibToken? _BuildMcLibToken(
        JsonObject library,
        string name,
        string? rootUrl,
        bool isLocal,
        string customMcFolder,
        McInstance? targetMcInstance)
    {
        string localPath;
        if (isLocal && targetMcInstance is not null)
            localPath =
                $@"{targetMcInstance.PathInstance}libraries\{name.AfterFirst(":").Replace(":", "-")}.jar";
        else
            localPath = McLibGet(name, customMcFolder: customMcFolder);

        var downloads = library["downloads"];
        var artifact = downloads?["artifact"];
        var artifactPath = artifact?["path"];
        try
        {
            if (artifact is not null)
                return new McLibToken
                {
                    OriginalName = name,
                    Url = rootUrl ?? (string?)artifact["url"],
                    LocalPath = artifactPath is null
                        ? McLibGet(name, customMcFolder: customMcFolder)
                        : McLibGetByArtifactPath(artifactPath.ToString(), customMcFolder),
                    size = (long)Math.Round(LauncherText.Val(artifact["size"]?.ToString() ?? "0")),
                    IsNatives = false,
                    Sha1 = artifact["sha1"]?.ToString(),
                    IsLocal = isLocal
                };

            return new McLibToken
            {
                OriginalName = name,
                Url = rootUrl,
                LocalPath = localPath,
                size = 0L,
                IsNatives = false,
                Sha1 = null,
                IsLocal = isLocal
            };
        }
        catch (Exception ex)
            when (artifactPath is not null && ex is ArgumentException or IOException)
        {
            LauncherCommon.Log(ex, $"支持库下载路径非法，已跳过（无 Natives，{name}）");
            return null;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"处理实际支持库列表失败（无 Natives，{name}）");
            return new McLibToken
            {
                OriginalName = name,
                Url = rootUrl,
                LocalPath = localPath,
                size = 0L,
                IsNatives = false,
                Sha1 = null
            };
        }
    }

    private static McLibToken? _BuildMcLibNativeToken(
        JsonObject library,
        string name,
        string? rootUrl,
        bool isLocal,
        string customMcFolder,
        string nativeName)
    {
        var classifiers = library["downloads"]?["classifiers"];
        var nativesWindows = classifiers?["natives-windows"];
        var nativePath = nativesWindows?["path"];
        var fallbackPath = McLibGet(name, customMcFolder: customMcFolder)
            .Replace(".jar", $"-{nativeName}.jar")
            .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32");
        try
        {
            if (nativesWindows is not null)
                return new McLibToken
                {
                    OriginalName = name,
                    Url = rootUrl ?? (string?)nativesWindows["url"],
                    LocalPath = nativePath is null
                        ? fallbackPath
                        : McLibGetByArtifactPath(nativePath.ToString(), customMcFolder),
                    size = (long)Math.Round(
                        LauncherText.Val(nativesWindows["size"]?.ToString() ?? "0")),
                    IsNatives = true,
                    Sha1 = nativesWindows["sha1"]?.ToString(),
                    IsLocal = isLocal
                };

            return new McLibToken
            {
                OriginalName = name,
                Url = rootUrl,
                LocalPath = fallbackPath,
                size = 0L,
                IsNatives = true,
                Sha1 = null,
                IsLocal = isLocal
            };
        }
        catch (Exception ex) when (nativePath is not null && ex is ArgumentException or IOException)
        {
            LauncherCommon.Log(ex, $"支持库下载路径非法，已跳过（有 Natives，{name}）");
            return null;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"处理实际支持库列表失败（有 Natives，{name}）");
            return new McLibToken
            {
                OriginalName = name,
                Url = rootUrl,
                LocalPath = McLibGet(name, customMcFolder: customMcFolder)
                    .Replace(".jar", $"-{nativeName}.jar")
                    .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                size = 0L,
                IsNatives = true,
                Sha1 = null
            };
        }
    }

    private static List<McLibToken> _DeduplicateMcLibTokens(
        List<McLibToken> basicArray,
        bool keepSameNameDifferentVersionResult)
    {
        var resultArray = new Dictionary<string, McLibToken>();
        foreach (var token in basicArray)
        {
            var key = token.Name + token.IsNatives;
            if (resultArray.TryGetValue(key, out var value))
            {
                var basicArrayVersion = _GetMcLibVersion(token) ?? "";
                var resultArrayVersion = _GetMcLibVersion(value) ?? "";
                if (basicArrayVersion != resultArrayVersion && keepSameNameDifferentVersionResult)
                {
                    LauncherCommon.Log(
                        $"[Minecraft] 发现疑似重复的支持库：{token} ({basicArrayVersion}) "
                        + $"与 {resultArray[key]} ({resultArrayVersion})");
                    resultArray.Add(key + LauncherCommon.GetUuid(), token);
                }
                else
                {
                    LauncherCommon.Log(
                        $"[Minecraft] 发现重复的支持库：{token} ({basicArrayVersion}) "
                        + $"与 {resultArray[key]} ({resultArrayVersion})，已忽略其中之一");
                    if (McVersionComparer.CompareVersionGe(basicArrayVersion, resultArrayVersion))
                        resultArray[key] = token;
                }
            }
            else
            {
                resultArray.Add(key, token);
            }
        }

        return [.. resultArray.Values];
    }

    private static string _GetMcLibVersion(McLibToken token)
    {
        return LauncherCommon.GetFolderNameFromPath(
            LauncherCommon.GetPathFromFullPath(token.LocalPath));
    }

    public class McLibToken
    {
        /// <summary>
        ///     是否为纯本地文件，若是则不尝试联网下载。
        /// </summary>
        public bool IsLocal;

        /// <summary>
        ///     是否为 Natives 文件。
        /// </summary>
        public bool IsNatives;

        /// <summary>
        ///     文件的完整本地路径。
        /// </summary>
        public string LocalPath = "";

        /// <summary>
        ///     原 JSON 中的 Name 项。
        /// </summary>
        public string OriginalName = "";

        /// <summary>
        ///     文件的 SHA1。
        /// </summary>
        public string? Sha1;

        /// <summary>
        ///     文件大小。若无有效数据即为 0。
        /// </summary>
        public long size;

        /// <summary>
        ///     由 JSON 提供的 URL，若没有则为 Nothing。
        /// </summary>
        public string? Url
        {
            get;
            set => field = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        ///     原 JSON 中 Name 项除去版本号部分的较前部分。可能为 Nothing。
        /// </summary>
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(OriginalName))
                    return "";
                var splited = new List<string>(OriginalName.Split(":"));
                splited.RemoveAt(2);
                return splited.Join(":");
            }
        }

        public override string ToString()
        {
            return
                $"{(IsNatives ? "[Native] " : "")}{LauncherCommon.GetString(size)} | {LocalPath}";
        }
    }
}