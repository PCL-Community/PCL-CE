using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;
using PCL.Network;

namespace PCL;

public static class ModLibrary
{
    public class McLibToken
    {
        private string _Url;

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
        public string LocalPath;

        /// <summary>
        ///     原 JSON 中的 Name 项。
        /// </summary>
        public string OriginalName;

        /// <summary>
        ///     文件的 SHA1。
        /// </summary>
        public string Sha1;

        /// <summary>
        ///     文件大小。若无有效数据即为 0。
        /// </summary>
        public long size;

        /// <summary>
        ///     由 JSON 提供的 URL，若没有则为 Nothing。
        /// </summary>
        public string Url
        {
            get => _Url;
            set =>
                // 孤儿 Forge 作者喜欢把没有 URL 的写个空字符串
                _Url = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        ///     原 JSON 中 Name 项除去版本号部分的较前部分。可能为 Nothing。
        /// </summary>
        public string Name
        {
            get
            {
                if (OriginalName is null)
                    return null;
                var splited = new List<string>(OriginalName.Split(":"));
                splited.RemoveAt(2); // Java 的此格式下版本号固定为第三段，第四段可能包含架构、分包等其他信息
                return splited.Join(":");
            }
        }

        public override string ToString()
        {
            return (IsNatives ? "[Native] " : "") + ModBase.GetString(size) + " | " + LocalPath;
        }
    }

    /// <summary>
    ///     检查是否符合 JSON 中的 Rules。
    /// </summary>
    /// <param name="ruleToken">JSON 中的 "rules" 项目。</param>
    public static bool McJsonRuleCheck(JsonNode ruleToken)
    {
        if (ruleToken is null)
            return true;

        // 初始化
        var required = false;
        foreach (var Rule in ruleToken.AsArray())
        {
            // 单条条件验证
            var isRightRule = true; // 是否为正确的规则
            if (Rule["os"] is not null) // 操作系统
            {
                if (Rule["os"]["name"] is not null) // 操作系统名称
                {
                    var osName = Rule["os"]["name"].ToString();
                    if (osName == "unknown")
                    {
                    }
                    else if (osName == "windows")
                    {
                        if (Rule["os"]["version"] is not null) // 操作系统版本
                        {
                            var cr = Rule["os"]["version"].ToString();
                            isRightRule = isRightRule && osVersion.RegexCheck(cr);
                        }
                    }
                    else
                    {
                        isRightRule = false;
                    }
                }

                if (Rule["os"]["arch"] is not null) // 操作系统架构
                    isRightRule = isRightRule && Rule["os"]["arch"].ToString() == "x86" == SystemInfo.Is32BitSystem;
            }

            if (Rule["features"] is not null) // 标签
            {
                isRightRule = isRightRule && Rule["features"]["is_demo_user"] is null; // 反选是否为 Demo 用户
                if (Rule["features"].AsObject().Any(prop => prop.Key.Contains("quick_play")))
                    isRightRule = false; // 不开 Quick Play，让玩家自己加去
            }

            // 反选确认
            if (Rule["action"].ToString() == "allow")
            {
                if (isRightRule)
                    required = true; // allow
            }
            else if (isRightRule)
            {
                required = false; // disallow
            }
        }

        return required;
    }

    private static readonly string osVersion = Environment.OSVersion.Version.ToString();

    /// <summary>
    ///     递归获取 Minecraft 某一实例的完整支持库列表。
    /// </summary>
    public static List<McLibToken> McLibListGet(McInstance mcInstance, bool includeInstanceJar)
    {
        // 获取当前支持库列表
        ModBase.Log("[Minecraft] 获取支持库列表：" + mcInstance.Name);
        var result = McLibListGetWithJson(mcInstance.JsonObject, targetMcInstance: mcInstance);

        // 需要添加原版 Jar
        if (includeInstanceJar)
        {
            McInstance realMcInstance;
            var requiredJar = mcInstance.JsonObject["jar"]?.ToString();
            if (mcInstance.IsHmclFormatJson || requiredJar is null)
            {
                // HMCL 项直接使用自身的 Jar
                // 根据 Inherit 获取最深层实例
                var originalInstance = mcInstance;
                // 1.17+ 的 Forge 不寻找 Inherit
                if (!((mcInstance.Info.HasForge || mcInstance.Info.HasNeoForge) && mcInstance.Info.Drop >= 170))
                    while (!string.IsNullOrEmpty(originalInstance.InheritInstanceName))
                    {
                        if ((originalInstance.InheritInstanceName ?? "") == (originalInstance.Name ?? ""))
                            break;
                        originalInstance = new McInstance(Path.Combine(ModFolder.mcFolderSelected, "versions", originalInstance.InheritInstanceName));
                    }

                // 需要新建对象，否则后面的 Check 会导致 McInstanceCurrent 的 State 变回 Original
                // 复现：启动一个 Snapshot 实例
                realMcInstance = new McInstance(originalInstance.PathInstance);
            }
            else
            {
                // Json 已提供 Jar 字段，使用该字段的信息
                realMcInstance = new McInstance(requiredJar);
            }

            string clientUrl;
            string clientSHA1;
            // 判断需求的实例是否存在
            // 不能调用 RealVersion.Check()，可能会莫名其妙地触发 CheckPermission 正被另一进程使用，导致误判前置不存在
            if (!File.Exists(realMcInstance.PathInstance + realMcInstance.Name + ".json"))
            {
                realMcInstance = mcInstance;
                ModBase.Log("[Minecraft] 可能缺少前置实例 " + realMcInstance.Name + "，找不到对应的 JSON 文件", ModBase.LogLevel.Debug);
            }

            // 获取详细下载信息
            if (realMcInstance.JsonObject["downloads"] is not null &&
                realMcInstance.JsonObject["downloads"]["client"] is not null)
            {
                clientUrl = (string)realMcInstance.JsonObject["downloads"]["client"]["url"];
                clientSHA1 = (string)realMcInstance.JsonObject["downloads"]["client"]["sha1"];
            }
            else
            {
                clientUrl = null;
                clientSHA1 = null;
            }

            // 把所需的原版 Jar 添加进去
            result.Add(new McLibToken
            {
                LocalPath = realMcInstance.PathInstance + realMcInstance.Name + ".jar", size = 0L, IsNatives = false,
                Url = clientUrl, Sha1 = clientSHA1
            });
        }

        return result;
    }

    /// <summary>
    ///     获取 Minecraft 某一实例忽视继承的支持库列表，即结果中没有继承项。
    /// </summary>
    public static List<McLibToken> McLibListGetWithJson(JsonObject jsonObject,
        bool keepSameNameDifferentVersionResult = false, string customMcFolder = null, McInstance targetMcInstance = null)
    {
        customMcFolder = customMcFolder ?? ModFolder.mcFolderSelected;
        var basicArray = new List<McLibToken>();

        // 添加基础 Json 项
        var allLibs = (JsonArray)jsonObject["libraries"];

        // 转换为 LibToken
        foreach (var LibraryNode in allLibs)
        {
            var library = LibraryNode.AsObject();
            // 清理 null 项（BakaXL 会把没有的项序列化为 null；这导致了 #409）
            var keysToRemove = library.Where(p => p.Value?.GetValueKind() == JsonValueKind.Null).Select(p => p.Key).ToList();
            foreach (var key in keysToRemove)
                library.Remove(key);

            // 检查是否需要（Rules）
            if (!McJsonRuleCheck(library["rules"]))
                continue;

            // 获取根节点下的 url
            var rootUrl = (string)library["url"];
            if (rootUrl is not null)
                rootUrl += McLibGet((string)library["name"], false, true, customMcFolder).Replace(@"\", "/");

            // 是否为纯本地项
            var hint = (string)library["hint"];
            var isLocal = hint is not null ? hint == "local" : false;

            // 根据是否本地化处理（Natives）
            if (library["natives"] is null) // 没有 Natives
            {
                string localPath;
                if (isLocal && targetMcInstance is not null) // 纯本地项
                    localPath = targetMcInstance.PathInstance + @"libraries\" +
                                library["name"].ToString().AfterFirst(":").Replace(":", "-") + ".jar";
                else
                    localPath = McLibGet((string)library["name"], customMcFolder: customMcFolder);
                var artifactPath = library["downloads"] is not null && library["downloads"]["artifact"] is not null
                    ? library["downloads"]["artifact"]["path"]
                    : null;
                try
                {
                    if (library["downloads"] is not null && library["downloads"]["artifact"] is not null)
                    {
                        var init = new McLibToken();
                        basicArray.Add((init.OriginalName = (string)library["name"],
                            init.Url = (string)(rootUrl ?? library["downloads"]["artifact"]["url"]),
                            init.LocalPath = artifactPath is null
                                ? McLibGet((string)library["name"], customMcFolder: customMcFolder)
                                : McLibGetByArtifactPath(artifactPath.ToString(), customMcFolder),
                            init.size = (long)Math.Round(
                                ModBase.Val(library["downloads"]["artifact"]["size"].ToString())),
                            init.IsNatives = false, init.Sha1 = library["downloads"]["artifact"]["sha1"]?.ToString(),
                            init.IsLocal = isLocal, init).init);
                    }
                    else
                    {
                        basicArray.Add(new McLibToken
                        {
                            OriginalName = (string)library["name"], Url = rootUrl, LocalPath = localPath, size = 0L,
                            IsNatives = false, Sha1 = null, IsLocal = isLocal
                        });
                    }
                }
                catch (Exception ex) when (artifactPath is not null && (ex is ArgumentException || ex is IOException))
                {
                    ModBase.Log(ex, "支持库下载路径非法，已跳过（无 Natives，" + (library["name"] ?? "Nothing") + "）");
                    continue;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "处理实际支持库列表失败（无 Natives，" + (library["name"] ?? "Nothing") + "）");
                    basicArray.Add(new McLibToken
                    {
                        OriginalName = (string)library["name"], Url = rootUrl, LocalPath = localPath, size = 0L,
                        IsNatives = false, Sha1 = null
                    });
                }
            }
            else if (library["natives"]["windows"] is not null) // 有 Windows Natives
            {
                var nativePath = library["downloads"] is not null && library["downloads"]["classifiers"] is not null &&
                                 library["downloads"]["classifiers"]["natives-windows"] is not null
                    ? library["downloads"]["classifiers"]["natives-windows"]["path"]
                    : null;
                try
                {
                    if (library["downloads"] is not null && library["downloads"]["classifiers"] is not null &&
                        library["downloads"]["classifiers"]["natives-windows"] is not null)
                        basicArray.Add(new McLibToken
                        {
                            OriginalName = (string)library["name"],
                            Url = (string)(rootUrl ?? library["downloads"]["classifiers"]["natives-windows"]["url"]),
                            LocalPath = nativePath is null
                                ? McLibGet((string)library["name"], customMcFolder: customMcFolder)
                                    .Replace(".jar", "-" + library["natives"]["windows"] + ".jar")
                                    .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32")
                                : McLibGetByArtifactPath(nativePath.ToString(), customMcFolder),
                            size = (long)Math.Round(
                                ModBase.Val(library["downloads"]["classifiers"]["natives-windows"]["size"].ToString())),
                            IsNatives = true,
                            Sha1 = library["downloads"]["classifiers"]["natives-windows"]["sha1"].ToString(),
                            IsLocal = isLocal
                        });
                    else
                        basicArray.Add(new McLibToken
                        {
                            OriginalName = (string)library["name"], Url = rootUrl,
                            LocalPath = McLibGet((string)library["name"], customMcFolder: customMcFolder)
                                .Replace(".jar", "-" + library["natives"]["windows"] + ".jar")
                                .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                            size = 0L, IsNatives = true, Sha1 = null, IsLocal = isLocal
                        });
                }
                catch (Exception ex) when (nativePath is not null && (ex is ArgumentException || ex is IOException))
                {
                    ModBase.Log(ex, "支持库下载路径非法，已跳过（有 Natives，" + (library["name"] ?? "Nothing") + "）");
                    continue;
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "处理实际支持库列表失败（有 Natives，" + (library["name"] ?? "Nothing") + "）");
                    basicArray.Add(new McLibToken
                    {
                        OriginalName = (string)library["name"], Url = rootUrl,
                        LocalPath = McLibGet((string)library["name"], customMcFolder: customMcFolder)
                            .Replace(".jar", "-" + library["natives"]["windows"] + ".jar")
                            .Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32"),
                        size = 0L, IsNatives = true, Sha1 = null, IsLocal = false
                    });
                }
            }
        }

        // 去重
        var resultArray = new Dictionary<string, McLibToken>();

        // 测试例：
        // D:\Minecraft\test\libraries\net\neoforged\mergetool\2.0.0\mergetool-2.0.0-api.jar
        // D:\Minecraft\test\libraries\org\apache\commons\commons-collections4\4.2\commons-collections4-4.2.jar
        // D:\Minecraft\test\libraries\com\google\guava\guava\31.1-jre\guava-31.1-jre.jar
        string GetVersion(McLibToken token)
        {
            return ModBase.GetFolderNameFromPath(ModBase.GetPathFromFullPath(token.LocalPath));
        }

        for (int i = 0, loopTo = basicArray.Count - 1; i <= loopTo; i++)
        {
            var key = basicArray[i].Name + basicArray[i].IsNatives;
            if (resultArray.ContainsKey(key))
            {
                var basicArrayVersion = GetVersion(basicArray[i]);
                var resultArrayVersion = GetVersion(resultArray[key]);
                if ((basicArrayVersion ?? "") != (resultArrayVersion ?? "") && keepSameNameDifferentVersionResult)
                {
                    ModBase.Log(
                        $"[Minecraft] 发现疑似重复的支持库：{basicArray[i]} ({basicArrayVersion}) 与 {resultArray[key]} ({resultArrayVersion})");
                    resultArray.Add(key + ModBase.GetUuid(), basicArray[i]);
                }
                else
                {
                    ModBase.Log(
                        $"[Minecraft] 发现重复的支持库：{basicArray[i]} ({basicArrayVersion}) 与 {resultArray[key]} ({resultArrayVersion})，已忽略其中之一");
                    if (McVersionComparer.CompareVersionGe(basicArrayVersion, resultArrayVersion)) resultArray[key] = basicArray[i];
                }
            }
            else
            {
                resultArray.Add(key, basicArray[i]);
            }
        }

        return resultArray.Values.ToList();
    }

    /// <summary>
    ///     获取实例所需支持库文件的 NetFile。
    /// </summary>
    public static List<DownloadFile> McLibNetFilesFromInstance(McInstance mcInstance)
    {
        if (!mcInstance.IsLoaded)
            mcInstance.Load();
        var result = new List<DownloadFile>();

        // 更新此方法时需要同步更新 Forge 新版自动安装方法！

        // 主 Jar 文件
        try
        {
            var mainJar = ModDownload.DlClientJarGet(mcInstance, true);
            if (mainJar is not null)
                result.Add(mainJar);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "实例缺失主 Jar 文件所必须的信息", ModBase.LogLevel.Developer);
        }

        // Library 文件
        result.AddRange(McLibNetFilesFromTokens(McLibListGet(mcInstance, false)));

        // Authlib-Injector 文件
        var authlibTargetFile = Path.Combine(ModBase.pathPure, "authlib-injector.jar");
        JsonObject authlibDownloadInfo = null;
        try
        {
            ModBase.Log("[Minecraft] 开始获取 Authlib-Injector 下载信息");
            authlibDownloadInfo = (JsonObject)ModBase.GetJson(ModNet.NetGetCodeByLoader(
                new[]
                {
                    "https://authlib-injector.yushi.moe/artifact/latest.json",
                    "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                }, isJson: true));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取 Authlib-Injector 下载信息失败");
        }

        // 校验文件
        if (authlibDownloadInfo is not null)
        {
            var checker = new ModBase.FileChecker(hash: authlibDownloadInfo["checksums"]["sha256"].ToString());
            if (checker.Check(authlibTargetFile) is not null)
            {
                // 开始下载
                var downloadAddress = authlibDownloadInfo["download_url"].ToString()
                    .Replace("bmclapi2.bangbang93.com/mirrors/authlib-injector", "authlib-injector.yushi.moe");
                ModBase.Log("[Minecraft] Authlib-Injector 需要更新：" + downloadAddress, ModBase.LogLevel.Developer);
                result.Add(new DownloadFile(
                    new[]
                    {
                        downloadAddress,
                        downloadAddress.Replace("authlib-injector.yushi.moe",
                            "bmclapi2.bangbang93.com/mirrors/authlib-injector")
                    }, authlibTargetFile,
                    new ModBase.FileChecker(hash: authlibDownloadInfo["checksums"]["sha256"].ToString())));
            }
        }

        // 修改渲染器
        var mesaLoaderWindowsTargetFile =
            Path.Combine(ModBase.pathPure, "mesa-loader-windows", ModLaunch.mesaLoaderWindowsVersion, "Loader.jar");
        var renderer = -1;
        if (ModInstanceList.McMcInstanceSelected is not null)
            renderer = Config.Instance.Renderer[ModInstanceList.McMcInstanceSelected?.PathInstance] - 1;
        if (renderer == -1) renderer = Config.Launch.Renderer;

        if (renderer != 0 && !File.Exists(mesaLoaderWindowsTargetFile))
        {
            var downloadAddress =
                "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/org/glavo/mesa-loader-windows/" +
                ModLaunch.mesaLoaderWindowsVersion + "/mesa-loader-windows-" + ModLaunch.mesaLoaderWindowsVersion + "-" +
                (SystemInfo.Is32BitSystem ? "x86" : SystemInfo.IsArm64System ? "arm64" : "x64") + ".jar";
            result.Add(new DownloadFile(new[] { downloadAddress }, mesaLoaderWindowsTargetFile));
        }

        // LabyMod Assets 文件
        if (mcInstance.Info.HasLabyMod)
        {
            if ((mcInstance.PathIndie ?? "") == (mcInstance.PathInstance ?? ""))
            {
                if (Directory.Exists(Path.Combine(mcInstance.PathInstance, "labymod-neo")))
                    Directory.Delete(Path.Combine(mcInstance.PathInstance, "labymod-neo"), true);
                ModBase.CreateSymbolicLink(Path.Combine(mcInstance.PathInstance, "labymod-neo"), Path.Combine(ModFolder.mcFolderSelected, "labymod-neo"),
                    0x2);
            }

            try
            {
                var channelType = mcInstance.JsonObject["labymod_data"]["channelType"].ToString();
                Directory.CreateDirectory($@"{ModFolder.mcFolderSelected}labymod-neo\libraries");
                ModBase.Log("[Minecraft] 开始获取 LabyMod 信息");
                var labyManifest = (JsonObject)ModNet.NetGetCodeByRequestRetry(
                    $"https://releases.r2.labymod.net/api/v1/manifest/{channelType}/latest.json", isJson: true);
                var labyAssets = (JsonObject)labyManifest["assets"];
                var labyModCommitRef = labyManifest["commitReference"].ToString();
                foreach (var Asset in labyAssets)
                {
                    var assetName = Asset.Key;
                    var assetSHA1 = Asset.Value.ToString();
                    var assetPath = $@"{ModFolder.mcFolderSelected}labymod-neo\assets\{assetName}.jar";
                    var assetUrl =
                        $"https://releases.r2.labymod.net/api/v1/download/assets/labymod4/{channelType}/{labyModCommitRef}/{assetName}/{assetSHA1}.jar";
                    var checker = new ModBase.FileChecker(hash: assetSHA1);
                    if (checker.Check(assetPath) is null)
                        continue;
                    result.Add(new DownloadFile(new[] { assetUrl }, assetPath, checker));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取 LabyMod 信息失败，跳过检查");
            }
        }

        // 跳过校验
        if (ShouldIgnoreFileCheck(mcInstance))
        {
            ModBase.Log("[Minecraft] 用户要求尽量忽略文件检查，这可能会保留有误的文件");
            result = result.Where(f =>
            {
                if (File.Exists(f.LocalPath))
                {
                    ModBase.Log("[Minecraft] 跳过下载的支持库文件：" + f.LocalPath, ModBase.LogLevel.Debug);
                    return false;
                }

                return true;
            }).ToList();
        }

        return result;
    }

    /// <summary>
    ///     将 McLibToken 列表转换为 NetFile。
    /// </summary>
    public static List<DownloadFile> McLibNetFilesFromTokens(List<McLibToken> libs, string customMcFolder = null)
    {
        customMcFolder = customMcFolder ?? ModFolder.mcFolderSelected;
        var result = new List<DownloadFile>();
        // 获取
        foreach (var token in libs)
        {
            // 检查文件
            var checker = new ModBase.FileChecker(actualSize: token.size == 0L ? -1 : token.size, hash: token.Sha1);
            if (checker.Check(token.LocalPath) is null)
                continue;
            if (token.IsLocal)
            {
                ModBase.Log("[Download] 已跳过被标记为本地文件的支持库: " + token.OriginalName);
                continue;
            }

            // URL
            var urls = new List<string>();
            if (token.Url is null && token.Name == "net.minecraftforge:forge:universal")
                // 特判修复 Forge 部分 universal 文件缺失 URL（#5455）
                token.Url = "https://maven.minecraftforge.net" +
                            token.LocalPath.Replace(customMcFolder + "libraries", "").Replace(@"\", "/");
            if (token.Url is not null)
            {
                // 获取 URL 的真实地址
                urls.Add(token.Url);
                if (token.Url.Contains("launcher.mojang.com/v1/objects") || token.Url.Contains("client.txt") ||
                    token.Url.Contains(".tsrg"))
                    urls.AddRange(ModDownload.DlSourceLauncherOrMetaGet(token.Url)); // Mappings（#4425）
                if (token.Url.Contains("maven"))
                {
                    var bmclapiUrl = token.Url
                        .Replace(token.Url.Substring(0, token.Url.IndexOfF("maven")),
                            "https://bmclapi2.bangbang93.com/").Replace("maven.fabricmc.net", "maven")
                        .Replace("maven.minecraftforge.net", "maven").Replace("maven.neoforged.net/releases", "maven");
                    if (ModDownload.DlSourcePreferMojang)
                        urls.Add(bmclapiUrl); // 官方源优先
                    else
                        urls.Insert(0, bmclapiUrl); // 镜像源优先
                }
            }

            if (token.LocalPath.Contains("transformer-discovery-service"))
            {
                // Transformer 文件释放
                if (!File.Exists(token.LocalPath))
                    ModBase.WriteFile(token.LocalPath, ModBase.GetResourceStream("Resources/transformer.jar"));
                ModBase.Log("[Download] 已自动释放 Transformer Discovery Service", ModBase.LogLevel.Developer);
                continue;
            }

            if (token.LocalPath.Contains(@"optifine\OptiFine"))
            {
                // OptiFine 主 Jar
                var optiFineBase =
                    token.LocalPath.Replace(Path.Combine(customMcFolder, "libraries", "optifine", "OptiFine") + @"\", "").Split("_")[0] + "/" +
                    ModBase.GetFileNameFromPath(token.LocalPath).Replace("-", "_");
                optiFineBase = "/maven/com/optifine/" + optiFineBase;
                if (optiFineBase.Contains("_pre"))
                    optiFineBase = optiFineBase.Replace("com/optifine/", "com/optifine/preview_");
                urls.Add("https://bmclapi2.bangbang93.com" + optiFineBase);
            }
            else if (token.Name.Contains("LabyMod"))
            {
                // LabyMod 只有一个下载源
                urls.Add(token.Url);
                ModBase.Log(
                    $"[Download] 获取到 LabyMod 主要库文件的 Size = {token.size},SHA1 = {token.Sha1}，由于 LabyMod 乱写 Size，已忽略 Size");
                checker = new ModBase.FileChecker(hash: token.Sha1); // 只校验 SHA1
            }
            else if (urls.Count <= 2)
            {
                // 普通文件
                urls.AddRange(ModDownload.DlSourceLibraryGet("https://libraries.minecraft.net" +
                                                             token.LocalPath.Replace(customMcFolder + "libraries", "")
                                                                 .Replace(@"\", "/")));
            }

            result.Add(new DownloadFile(urls.Distinct(), token.LocalPath, checker));
        }

        // 去重并返回
        return result.Distinct((a, b) => (a.LocalPath ?? "") == (b.LocalPath ?? ""));
    }

    /// <summary>
    ///     获取对应的支持库文件地址。
    /// </summary>
    /// <param name="original">原始地址，如 com.mumfrey:liteloader:1.12.2-SNAPSHOT。</param>
    /// <param name="withHead">是否包含 Lib 文件夹头部，若不包含，则会类似以 com\xxx\ 开头。</param>
    public static string McLibGet(string original, bool withHead = true, bool ignoreLiteLoader = false,
        string customMcFolder = null)
    {
        string mcLibGetRet = default;
        customMcFolder = customMcFolder ?? ModFolder.mcFolderSelected;
        var splited = original.Split(":");
        mcLibGetRet = withHead
            ? Path.Combine(customMcFolder, "libraries", splited[0].Replace(".", @"\"), splited[1], splited[2], splited[1] + "-" + splited[2] + ".jar")
            : Path.Combine(splited[0].Replace(".", @"\"), splited[1], splited[2], splited[1] + "-" + splited[2] + ".jar");
        // 判断 OptiFine 是否应该使用 installer
        if (mcLibGetRet.Contains(@"optifine\OptiFine\1.") && splited[2].Split(".").Count() > 1)
        {
            var majorVersion = (int)Math.Round(ModBase.Val(splited[2].Split(".")[1].BeforeFirst("_")));
            var minorVersion = (int)Math.Round(splited[2].Split(".").Count() > 2
                ? ModBase.Val(splited[2].Split(".")[2].BeforeFirst("_"))
                : 0d);
            if ((majorVersion == 12 || (majorVersion == 20 && minorVersion >= 4) || majorVersion >= 21) && File.Exists(
                    $@"{customMcFolder}libraries\{splited[0].Replace(".", @"\")}\{splited[1]}\{splited[2]}\{splited[1]}-{splited[2]}-installer.jar")) // 仅在 1.12 (无法追溯) 和 1.20.4+ (#5376) 遇到此问题
            {
                ModLaunch.McLaunchLog("已将 " + original + " 替换为对应的 Installer 文件");
                mcLibGetRet = mcLibGetRet.Replace(".jar", "-installer.jar");
            }
        }

        return mcLibGetRet;
    }

    /// <summary>
    ///     根据 JSON 中的 downloads.artifact.path 获取支持库文件地址。
    /// </summary>
    private static string McLibGetByArtifactPath(string artifactPath, string customMcFolder = null)
    {
        customMcFolder = customMcFolder ?? ModFolder.mcFolderSelected;
        if (string.IsNullOrWhiteSpace(artifactPath))
            throw new ArgumentException("支持库下载路径无效", nameof(artifactPath));

        var librariesRoot = Path.GetFullPath(Path.Combine(customMcFolder, "libraries"));
        var normalizedArtifactPath = artifactPath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedArtifactPath))
            throw new IOException("支持库下载路径不能为绝对路径: " + artifactPath);

        var localPath = Path.GetFullPath(Path.Combine(librariesRoot, normalizedArtifactPath));
        var librariesRootWithSeparator = librariesRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                         Path.DirectorySeparatorChar;
        if (!localPath.StartsWith(librariesRootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new IOException("支持库下载路径不能位于 libraries 文件夹外: " + artifactPath);

        return localPath;
    }

    /// <summary>
    ///     检查设置，是否应当忽略文件检查？
    /// </summary>
    public static bool ShouldIgnoreFileCheck(McInstance version)
    {
        return Config.Instance.DisableAssetVerifyV2[version.PathInstance] ||
               Config.Instance.AssetVerifySolutionV1[version.PathInstance] == 2;
    }
}
