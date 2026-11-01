using System.IO;
using PCL.Core.App;
using PCL.Core.Utils.OS;
using PCL.Network;

namespace PCL;

public static partial class Libraries
{
    /// <summary>
    ///     获取实例所需支持库文件的 NetFile。
    /// </summary>
    public static List<DownloadFile> McLibNetFilesFromInstance(McInstance mcInstance)
    {
        if (!mcInstance.IsLoaded)
            mcInstance.Load();
        var result = new List<DownloadFile>();

        _AddInstanceMainJarFile(result, mcInstance);

        result.AddRange(McLibNetFilesFromTokens(McLibListGet(mcInstance, false)));

        var authlibInjectorFile = _GetAuthlibInjectorFile();
        if (authlibInjectorFile is not null)
            result.Add(authlibInjectorFile);

        var mesaLoaderFile = _GetMesaLoaderFile();
        if (mesaLoaderFile is not null)
            result.Add(mesaLoaderFile);

        if (mcInstance.Info.HasLabyMod)
            _AddLabyModFiles(result, mcInstance);

        if (ShouldIgnoreFileCheck(mcInstance))
            result = _FilterIgnoredFiles(result);

        return result;
    }

    private static void _AddInstanceMainJarFile(List<DownloadFile> result, McInstance mcInstance)
    {
        try
        {
            var mainJar = GameDownload.DlClientJarGet(mcInstance, true);
            if (mainJar is not null)
                result.Add(mainJar);
        }
        catch (Exception ex)
        {
            Common.Log(ex, "实例缺失主 Jar 文件所必须的信息", Common.LogLevel.Developer);
        }
    }

    private static DownloadFile? _GetAuthlibInjectorFile()
    {
        var authlibTargetFile = Path.Combine(Common.pathPure, "authlib-injector.jar");
        var authlibDownloadInfo = _FetchAuthlibInjectorInfo();
        return authlibDownloadInfo is null
            ? null
            : _BuildAuthlibInjectorFile(authlibDownloadInfo, authlibTargetFile);
    }

    private static JsonObject? _FetchAuthlibInjectorInfo()
    {
        try
        {
            Common.Log("[Minecraft] 开始获取 Authlib-Injector 下载信息");
            return (JsonObject)
                Common.GetJson(
                    NetFacade.NetGetCodeByLoader(
                        [
                            "https://authlib-injector.yushi.moe/artifact/latest.json",
                            "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json"
                        ],
                        isJson: true));
        }
        catch (Exception ex)
        {
            Common.Log(ex, "获取 Authlib-Injector 下载信息失败");
            return null;
        }
    }

    private static DownloadFile? _BuildAuthlibInjectorFile(
        JsonObject authlibDownloadInfo,
        string authlibTargetFile)
    {
        var sha256 = authlibDownloadInfo["checksums"]?["sha256"]?.ToString() ?? "";
        var checker = new Common.FileChecker(hash: sha256);
        if (checker.Check(authlibTargetFile) is null)
            return null;

        var downloadAddress = (authlibDownloadInfo["download_url"]?.ToString() ?? "").Replace(
            "bmclapi2.bangbang93.com/mirrors/authlib-injector",
            "authlib-injector.yushi.moe");
        Common.Log(
            $"[Minecraft] Authlib-Injector 需要更新：{downloadAddress}",
            Common.LogLevel.Developer);
        return new DownloadFile(
            [
                downloadAddress,
                downloadAddress.Replace(
                    "authlib-injector.yushi.moe",
                    "bmclapi2.bangbang93.com/mirrors/authlib-injector"
                )
            ],
            authlibTargetFile,
            new Common.FileChecker(hash: sha256));
    }

    private static DownloadFile? _GetMesaLoaderFile()
    {
        var mesaLoaderWindowsTargetFile = Path.Combine(
            Common.pathPure,
            "mesa-loader-windows",
            GameLaunch.MesaLoaderWindowsVersion,
            "Loader.jar");
        var renderer = -1;
        var selectedInstance = InstanceList.McMcInstanceSelected;
        if (selectedInstance is not null)
            renderer = Config.Instance.Renderer[selectedInstance.PathInstance] - 1;
        if (renderer == -1)
            renderer = Config.Launch.Renderer;

        if (renderer == 0 || File.Exists(mesaLoaderWindowsTargetFile))
            return null;

        var arch =
            SystemInfo.Is32BitSystem ? "x86"
            : SystemInfo.IsArm64System ? "arm64"
            : "x64";
        var downloadAddress =
            "https://mirrors.cloud.tencent.com/nexus/repository/maven-public/"
            + $"org/glavo/mesa-loader-windows/{GameLaunch.MesaLoaderWindowsVersion}/"
            + $"mesa-loader-windows-{GameLaunch.MesaLoaderWindowsVersion}-{arch}.jar";
        return new DownloadFile([downloadAddress], mesaLoaderWindowsTargetFile);
    }

    private static void _AddLabyModFiles(List<DownloadFile> result, McInstance mcInstance)
    {
        _EnsureLabyModSymlink(mcInstance);

        try
        {
            var channelType =
                mcInstance.JsonObject["labymod_data"]?["channelType"]?.ToString() ?? "";
            Directory.CreateDirectory($@"{GameFolders.mcFolderSelected}labymod-neo\libraries");
            Common.Log("[Minecraft] 开始获取 LabyMod 信息");
            var labyManifest = (JsonObject)
                NetFacade.NetGetCodeByRequestRetry(
                    $"https://releases.r2.labymod.net/api/v1/manifest/{channelType}/latest.json",
                    isJson: true);
            var labyAssets = labyManifest["assets"] as JsonObject ?? [];
            var labyModCommitRef = labyManifest["commitReference"]?.ToString() ?? "";
            foreach (var (assetName, value) in labyAssets)
            {
                var assetFile = _CreateLabyModAssetFile(
                    assetName,
                    value,
                    channelType,
                    labyModCommitRef);
                if (assetFile is not null)
                    result.Add(assetFile);
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "获取 LabyMod 信息失败，跳过检查");
        }
    }

    private static void _EnsureLabyModSymlink(McInstance mcInstance)
    {
        if ((mcInstance.PathIndie ?? "") != (mcInstance.PathInstance ?? ""))
            return;

        var labymodFolder = Path.Combine(mcInstance.PathInstance ?? "", "labymod-neo");
        if (Directory.Exists(labymodFolder))
            Directory.Delete(labymodFolder, true);
        Common.CreateSymbolicLink(
            labymodFolder,
            Path.Combine(GameFolders.mcFolderSelected ?? "", "labymod-neo"));
    }

    private static DownloadFile? _CreateLabyModAssetFile(
        string assetName,
        JsonNode? value,
        string channelType,
        string labyModCommitRef)
    {
        var assetSha1 = value?.ToString() ?? "";
        var assetPath = $@"{GameFolders.mcFolderSelected}labymod-neo\assets\{assetName}.jar";
        var assetUrl =
            "https://releases.r2.labymod.net/api/v1/download/assets/labymod4/"
            + $"{channelType}/{labyModCommitRef}/{assetName}/{assetSha1}.jar";
        var checker = new Common.FileChecker(hash: assetSha1);
        return checker.Check(assetPath) is null
            ? null
            : new DownloadFile([assetUrl], assetPath, checker);
    }

    private static List<DownloadFile> _FilterIgnoredFiles(List<DownloadFile> files)
    {
        Common.Log("[Minecraft] 用户要求尽量忽略文件检查，这可能会保留有误的文件");
        return
        [
            .. files.Where(f =>
            {
                if (!File.Exists(f.LocalPath))
                    return true;

                Common.Log(
                    $"[Minecraft] 跳过下载的支持库文件：{f.LocalPath}",
                    Common.LogLevel.Debug);
                return false;
            })
        ];
    }

    /// <summary>
    ///     将 McLibToken 列表转换为 NetFile。
    /// </summary>
    public static List<DownloadFile> McLibNetFilesFromTokens(
        List<McLibToken> libs,
        string? customMcFolder = null)
    {
        customMcFolder ??= GameFolders.mcFolderSelected ?? "";
        var result = new List<DownloadFile>();
        foreach (var token in libs)
        {
            var checker = new Common.FileChecker(
                actualSize: token.size == 0L ? -1 : token.size,
                hash: token.Sha1);
            if (checker.Check(token.LocalPath) is null)
                continue;
            if (token.IsLocal)
            {
                Common.Log($"[Download] 已跳过被标记为本地文件的支持库: {token.OriginalName}");
                continue;
            }

            var urls = _GetLibraryUrls(token, customMcFolder);

            if (token.LocalPath.Contains("transformer-discovery-service"))
            {
                _ReleaseTransformerDiscoveryService(token);
                continue;
            }

            if (token.LocalPath.Contains(@"optifine\OptiFine"))
            {
                _AddOptiFineMirrorUrl(urls, token, customMcFolder);
            }
            else if (token.Name.Contains("LabyMod"))
            {
                if (token.Url is not null)
                    urls.Add(token.Url);
                Common.Log(
                    $"[Download] 获取到 LabyMod 主要库文件的 Size = {token.size},"
                    + $"SHA1 = {token.Sha1}，由于 LabyMod 乱写 Size，已忽略 Size");
                checker = new Common.FileChecker(hash: token.Sha1);
            }
            else if (urls.Count <= 2)
            {
                _AddMojangLibraryFallback(urls, token, customMcFolder);
            }

            result.Add(new DownloadFile(urls.Distinct(), token.LocalPath, checker));
        }

        return result.Distinct((a, b) => a.LocalPath == b.LocalPath);
    }

    private static void _ReleaseTransformerDiscoveryService(McLibToken token)
    {
        var transformerStream = Common.GetResourceStream("Resources/transformer.jar");
        if (!File.Exists(token.LocalPath) && transformerStream is not null)
            Common.WriteFile(token.LocalPath, transformerStream);
        Common.Log(
            "[Download] 已自动释放 Transformer Discovery Service",
            Common.LogLevel.Developer);
    }

    private static void _AddOptiFineMirrorUrl(
        List<string> urls,
        McLibToken token,
        string customMcFolder)
    {
        var optiFineFolder =
            $@"{Path.Combine(customMcFolder, "libraries", "optifine", "OptiFine")}\";
        var optiFineBase =
            token.LocalPath.Replace(optiFineFolder, "").Split("_")[0]
            + "/"
            + Common.GetFileNameFromPath(token.LocalPath).Replace("-", "_");
        optiFineBase = $"/maven/com/optifine/{optiFineBase}";
        if (optiFineBase.Contains("_pre"))
            optiFineBase = optiFineBase.Replace("com/optifine/", "com/optifine/preview_");
        urls.Add($"https://bmclapi2.bangbang93.com{optiFineBase}");
    }

    private static void _AddMojangLibraryFallback(
        List<string> urls,
        McLibToken token,
        string customMcFolder)
    {
        var relativeLibraryPath = token
            .LocalPath.Replace($"{customMcFolder}libraries", "")
            .Replace(@"\", "/");
        urls.AddRange(
            GameDownload.DlSourceLibraryGet(
                $"https://libraries.minecraft.net{relativeLibraryPath}"));
    }

    private static List<string> _GetLibraryUrls(McLibToken token, string customMcFolder)
    {
        var urls = new List<string>();
        if (token.Url is null && token.Name == "net.minecraftforge:forge:universal")
            token.Url =
                "https://maven.minecraftforge.net"
                + token.LocalPath.Replace($"{customMcFolder}libraries", "").Replace(@"\", "/");
        var url = token.Url;
        if (url is null)
            return urls;

        urls.Add(url);
        if (url.Contains("launcher.mojang.com/v1/objects")
            || url.Contains("client.txt")
            || url.Contains(".tsrg"))
            urls.AddRange(GameDownload.DlSourceLauncherOrMetaGet(url));

        if (!url.Contains("maven"))
            return urls;

        var bmclapiUrl = url
            .Replace(
                url[..url.IndexOfF("maven")],
                "https://bmclapi2.bangbang93.com/")
            .Replace("maven.fabricmc.net", "maven")
            .Replace("maven.minecraftforge.net", "maven")
            .Replace("maven.neoforged.net/releases", "maven");
        if (GameDownload.DlSourcePreferMojang)
            urls.Add(bmclapiUrl);
        else
            urls.Insert(0, bmclapiUrl);

        return urls;
    }
}