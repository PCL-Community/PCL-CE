using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;
using PCL.Network.Loaders;

namespace PCL;

public static partial class GameDownload
{
    public enum AssetsIndexExistsBehaviour
    {
        /// <summary>
        ///     如果文件存在，则不进行下载。
        /// </summary>
        DontDownload,

        /// <summary>
        ///     如果文件存在，则启动新的下载加载器进行独立的更新。
        /// </summary>
        DownloadInBackground,

        /// <summary>
        ///     如果文件存在，也同样进行下载。
        /// </summary>
        AlwaysDownload
    }

    private static readonly Lock AllDropsLock = new();

    /// <summary>
    ///     Minecraft 客户端 版本列表，主加载器。
    ///     若要求镜像源必须包含某个版本，则将该版本 ID 作为输入。
    /// </summary>
    public static Loading.LoaderTask<string, DlClientListResult> dlClientListLoader =
        new("DlClientList Main", DlClientListMain);

    /// <summary>
    ///     Minecraft 客户端 版本列表，Mojang 官方源加载器。
    /// </summary>
    public static Loading.LoaderTask<string, DlClientListResult> dlClientListMojangLoader =
        new("DlClientList Mojang", DlClientListMojangMain);

    private static bool _isNewClientVersionHinted = false;

    private static bool _dlClientListMojangMainIsHinted;

    /// <summary>
    ///     Minecraft 客户端 版本列表，BMCLAPI 源加载器。
    /// </summary>
    public static Loading.LoaderTask<string, DlClientListResult> dlClientListBmclapiLoader =
        new("DlClientList Bmclapi", DlClientListBmclapiMain);

    /// <summary>
    ///     所有正式版的 Minecraft Drop 序数。
    ///     若从未完成过获取，返回 Nothing；否则必定存在元素，且从高到低排列。
    /// </summary>
    public static List<int>? AllDrops
    {
        get
        {
            lock (AllDropsLock)
            {
                field ??= _LoadAllDrops();
                return field.Count != 0 ? field : null;
            }
        }
        set
        {
            lock (AllDropsLock)
            {
                field = value;
                States.Game.Drops = (value ?? []).Join(",");
            }
        }
    }

    private static List<int> _LoadAllDrops()
    {
        var rawData = States.Game.Drops;
        if (string.IsNullOrEmpty(rawData))
            return [];

        return
        [
            .. rawData
                .Split([","], StringSplitOptions.RemoveEmptyEntries)
                .Select(d => (int)Math.Round(LauncherText.Val(d)))
        ];
    }

    /// <summary>
    ///     返回某 Minecraft 版本对应的原版主 Jar 文件的下载信息，要求对应依赖实例已存在。
    ///     失败则抛出异常，不需要下载则返回 Nothing。
    /// </summary>
    public static DownloadFile? DlClientJarGet(McInstance version, bool returnNothingOnFileUseable)
    {
        version = _ResolveBaseInstance(version);

        var versionJson = version.JsonObject;
        var clientDownload = versionJson["downloads"]?["client"];
        var clientUrlNode = clientDownload?["url"];
        if (clientUrlNode is null)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.NoJarDownloadInfo",
                    version.Name));

        var checker = new FileChecker(
            1024L,
            clientDownload?["size"]?.ToObject<long>() ?? -1L,
            clientDownload?["sha1"]?.ToString() ?? "");

        var jarPath = $"{version.PathInstance}{version.Name}.jar";
        if (returnNothingOnFileUseable && checker.Check(jarPath) is null)
            return null;

        var jarUrl = clientUrlNode.ToString();
        return new DownloadFile(
            DlSourceLauncherOrMetaGet(jarUrl),
            jarPath,
            checker);
    }

    private static McInstance _ResolveBaseInstance(McInstance version)
    {
        try
        {
            while (!string.IsNullOrEmpty(version.InheritInstanceName))
                version = new McInstance(version.InheritInstanceName);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "获取底层继承实例失败");
        }

        return version;
    }

    /// <summary>
    ///     返回某 Minecraft 版本对应的原版主 AssetIndex 文件的下载信息，要求对应依赖实例已存在。
    ///     若未找到，则会返回 Legacy 资源文件或 Nothing。
    /// </summary>
    public static DownloadFile? DlClientAssetIndexGet(McInstance version)
    {
        while (!string.IsNullOrEmpty(version.InheritInstanceName))
            version = new McInstance(version.InheritInstanceName);

        var indexInfo = GameAssets.McAssetsGetIndex(
            version,
            true,
            true);
        if (string.IsNullOrEmpty(GameFolders.mcFolderSelected))
            return null;
        var indexId = $"{indexInfo["id"]}";
        var indexAddress = Path.Combine(
            GameFolders.mcFolderSelected,
            "assets",
            "indexes",
            $"{indexId}.json");

        LauncherCommon.Log($"[Download] 实例 {version.Name} 对应的资源文件索引为 {indexId}");
        var indexUrl = indexInfo["url"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(indexUrl))
            return null;

        var indexSize = indexInfo["size"]?.ToObject<long>() ?? -1L;
        return new DownloadFile(
            DlSourceLauncherOrMetaGet(indexUrl),
            indexAddress,
            new FileChecker(actualSize: indexSize, canUseExistsFile: false));
    }

    /// <summary>
    ///     构造补全某 Minecraft 版本的所有文件的加载器列表。失败会抛出异常。
    /// </summary>
    public static List<Loading.LoaderBase> DlClientFix(
        McInstance version,
        bool checkAssetsHash,
        AssetsIndexExistsBehaviour assetsIndexBehaviour)
    {
        var loaders = new List<Loading.LoaderBase>();
        _AddLibraryLoaders(loaders, version);
        _AddAssetsLoaders(loaders, version, checkAssetsHash, assetsIndexBehaviour);
        return loaders;
    }

    private static void _AddLibraryLoaders(
        List<Loading.LoaderBase> loaders,
        McInstance version)
    {
        if (Libraries.ShouldIgnoreFileCheck(version))
        {
            LauncherCommon.Log("[Download] 已跳过所有 Libraries 检查");
            return;
        }

        var loadersLib = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeMissingLibraries"),
                task => task.output = Libraries.McLibNetFilesFromInstance(version))
            {
                ProgressWeight = 1d
            },
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadLibraries"),
                [])
            {
                ProgressWeight = 15d
            }
        };

        loaders.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.DownloadLibraries.MainLoader"),
                loadersLib)
            {
                block = false,
                show = false,
                ProgressWeight = 16d
            });
    }

    private static void _AddAssetsLoaders(
        List<Loading.LoaderBase> loaders,
        McInstance version,
        bool checkAssetsHash,
        AssetsIndexExistsBehaviour assetsIndexBehaviour)
    {
        if (Libraries.ShouldIgnoreFileCheck(version))
        {
            LauncherCommon.Log("[Download] 已跳过所有 Assets 检查");
            return;
        }

        var loadersAssets = new List<Loading.LoaderBase>
        {
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex"),
                task => task.output = _GetClientAssetIndexFiles(version, assetsIndexBehaviour))
            {
                ProgressWeight = 0.5d,
                show = false
            },
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex"),
                [])
            {
                ProgressWeight = 2d
            }
        };

        if (assetsIndexBehaviour == AssetsIndexExistsBehaviour.DownloadInBackground)
            _StartAssetsIndexBackgroundUpdate(version);

        loadersAssets.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeMissingAssets"),
                task => task.output = _GetMissingAssets(version, checkAssetsHash, task))
            {
                ProgressWeight = 3d
            });

        loadersAssets.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadAssets"),
                [])
            {
                ProgressWeight = 25d
            });

        loaders.Add(
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Download.Stage.DownloadAssets.MainLoader"),
                loadersAssets)
            {
                block = false,
                show = false,
                ProgressWeight = 30.5d
            });
    }

    private static List<DownloadFile> _GetClientAssetIndexFiles(
        McInstance version,
        AssetsIndexExistsBehaviour assetsIndexBehaviour)
    {
        try
        {
            var indexFile = DlClientAssetIndexGet(version);
            if (indexFile is null) return [];
            _ = new FileInfo(indexFile.LocalPath);
            if (assetsIndexBehaviour != AssetsIndexExistsBehaviour.AlwaysDownload
                && indexFile.Check?.Check(indexFile.LocalPath) is null)
                return [];
            return [indexFile];
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.AssetIndexAnalysisFailed"),
                ex);
        }
    }

    private static List<DownloadFile> _GetMissingAssets(
        McInstance version,
        bool checkAssetsHash,
        Loading.LoaderTask<string, List<DownloadFile>> task)
    {
        Loading.LoaderBase progressFeed = task;
        return GameAssets.McAssetsFixList(
            version,
            checkAssetsHash,
            ref progressFeed);
    }

    private static void _StartAssetsIndexBackgroundUpdate(McInstance version)
    {
        var loadersAssetsUpdate = new List<Loading.LoaderBase>();
        string? tempAddress = null;
        string? realAddress = null;

        loadersAssetsUpdate.Add(
            new Loading.LoaderTask<string, List<DownloadFile>>(
                Lang.Text("Minecraft.Download.Stage.AnalyzeAssetsIndex.Background"),
                task =>
                {
                    var backAssetsFile = DlClientAssetIndexGet(version);
                    if (backAssetsFile is null) return;
                    realAddress = backAssetsFile.LocalPath;
                    tempAddress = $@"{LauncherPaths.Temp}Cache\{backAssetsFile.LocalName}";
                    backAssetsFile.LocalPath = tempAddress;
                    task.output = [backAssetsFile];
                    if (!_IsFileFromToday(realAddress))
                        return;
                    LauncherCommon.Log("[Download] 无需更新资源文件索引，取消");
                    task.Abort();
                }));

        loadersAssetsUpdate.Add(
            new LoaderDownload(
                Lang.Text("Minecraft.Download.Stage.DownloadAssetsIndex.Background"),
                []));

        loadersAssetsUpdate.Add(
            new Loading.LoaderTask<List<DownloadFile>, string>(
                Lang.Text("Minecraft.Download.Stage.CopyAssetsIndex.Background"),
                _ =>
                {
                    if (tempAddress is null || realAddress is null) return;
                    LauncherCommon.CopyFile(tempAddress, realAddress);
                    LauncherCommon.Log($"后台更新资源文件索引成功：{tempAddress}");
                }));

        var updater = new Loading.LoaderCombo<string>(
            Lang.Text("Minecraft.Download.Stage.UpdateAssetsIndex.Background"),
            loadersAssetsUpdate);
        LauncherCommon.Log("[Download] 开始后台检查资源文件索引");
        updater.Start();
    }

    private static bool _IsFileFromToday(string path)
    {
        return File.Exists(path)
            && Math.Abs((File.GetLastWriteTime(path).Date - DateTime.Now.Date).TotalDays) < 1d;
    }

    /// <summary>
    ///     只读取本地文件，判断实例是否可以在不下载 Minecraft 文件的情况下启动。
    /// </summary>
    public static bool McClientFilesAvailableLocally(McInstance instance)
    {
        try
        {
            if (!instance.IsLoaded)
                instance.Load();
            return File.Exists(Path.Combine(instance.PathInstance, $"{instance.Name}.json"))
                && _HasAllLocalLibraries(instance)
                && _HasAllLocalAssets(instance);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "本地检查 Minecraft 文件失败");
            return false;
        }
    }

    private static bool _HasAllLocalLibraries(McInstance instance)
    {
        foreach (var library in Libraries.McLibListGet(instance, true))
        {
            if (library.IsLocal)
                continue;

            var checker = new FileChecker(
                1,
                library.size > 0 ? library.size : -1,
                library.Sha1);
            if (checker.Check(library.LocalPath) is not null)
                return false;
        }

        return true;
    }

    private static bool _HasAllLocalAssets(McInstance instance)
    {
        return GameAssets
            .McAssetsListGet(instance)
            .All(asset =>
            {
                if (!File.Exists(asset.localPath))
                    return false;

                var length = new FileInfo(asset.localPath).Length;
                return length > 0 && (asset.size <= 0 || length == asset.size);
            });
    }

    private static void DlClientListMain(Loading.LoaderTask<string, DlClientListResult> loader)
    {
        DlSourceLoader(
            loader,
            _GetClientListSources(),
            loader.isForceRestarting);

        var drops = (loader.output.Value["versions"]?.AsArray() ?? [])
            .OfType<JsonObject>()
            .Select(version => McInstanceInfo.VersionToDrop(version["id"]?.ToString() ?? ""))
            .ToList();
        AllDrops = [.. drops.Distinct().OrderByDescending(d => d)];
    }

    private static List<KeyValuePair<Loading.LoaderTask<string, DlClientListResult>, int>>
        _GetClientListSources()
    {
        return Config.Download.VersionListSource switch
        {
            0 =>
            [
                new KeyValuePair<Loading.LoaderTask<string, DlClientListResult>, int>(
                    dlClientListBmclapiLoader,
                    30),
                new KeyValuePair<Loading.LoaderTask<string, DlClientListResult>, int>(
                    dlClientListMojangLoader,
                    30 + 60)
            ],
            1 =>
            [
                new KeyValuePair<Loading.LoaderTask<string, DlClientListResult>, int>(
                    dlClientListMojangLoader,
                    5),
                new KeyValuePair<Loading.LoaderTask<string, DlClientListResult>, int>(
                    dlClientListBmclapiLoader,
                    5 + 30)
            ],
            _ =>
            [
                new KeyValuePair<Loading.LoaderTask<string, DlClientListResult>, int>(
                    dlClientListMojangLoader,
                    60),
                new KeyValuePair<Loading.LoaderTask<string, DlClientListResult>, int>(
                    dlClientListBmclapiLoader,
                    60 + 60)
            ]
        };
    }

    private static void DlClientListMojangMain(
        Loading.LoaderTask<string, DlClientListResult> loader)
    {
        var startTime = TimeUtils.GetTimeTick();
        var json = (JsonObject)Requester.FetchJson(
            "https://launchermeta.mojang.com/mc/game/version_manifest.json");
        try
        {
            var versions = json["versions"] as JsonArray ?? [];
            _ValidateVersionList(versions, "Mojang", json);
            _MergeUnlistedVersions(versions, "官方源");

            _UpdateMojangSourcePreference(startTime);

            loader.output = new DlClientListResult
            {
                IsOfficial = true,
                SourceName = Lang.Text("Download.Source.MojangOfficial"),
                Value = json
            };
            _NotifyNewMinecraftVersions(json);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Mojang", ""),
                ex);
        }
    }

    private static void _ValidateVersionList(JsonArray versions, string sourceName, JsonObject json)
    {
        if (versions.Count < 200)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    sourceName,
                    json));
    }

    private static void _MergeUnlistedVersions(JsonArray versions, string sourceDescription)
    {
        var cacheFilePath = $@"{LauncherPaths.Temp}Cache\uvmc-download.json";
        if (!File.Exists(cacheFilePath))
            try
            {
                var unlistedJson = (JsonObject)Requester.FetchJson(
                    "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto/version_manifest.json");
                File.WriteAllText(cacheFilePath, unlistedJson.ToString());
            }
            catch (Exception ex)
            {
                LauncherCommon.Log($"[Download] 未列出的版本{sourceDescription}下载失败: {ex.Message}");
            }

        try
        {
            var cachedJson =
                (JsonObject)LauncherCommon.GetJson(LauncherCommon.ReadFile(cacheFilePath));
            versions.Merge(cachedJson["versions"]);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "[Download] UVMC 列表加载失败，忽略列表内容");
        }
    }

    private static void _UpdateMojangSourcePreference(long startTime)
    {
        if (DownloadSources.PreferMojang)
            return;

        var deltaTime = TimeUtils.GetTimeTick() - startTime;
        DownloadSources.PreferMojang = deltaTime < 4000;
        LauncherCommon.Log(
            $"[Download] Mojang 官方源加载耗时：{deltaTime}ms，{(DownloadSources.PreferMojang ? "可优先使用官方源" : "不优先使用官方源")}");
    }

    private static void _NotifyNewMinecraftVersions(JsonObject json)
    {
        var snapshotVersion = json["latest"]?["snapshot"]?.ToString();
        if (_ShouldHintNewVersion(
                Config.Tool.SnapshotNotification,
                States.Tool.LastSnapshot,
                snapshotVersion ?? ""))
        {
            _dlClientListMojangMainIsHinted = true;
            DownloadUserInteraction.Current?.ShowClientUpdateHint(snapshotVersion ?? "", json);
        }

        States.Tool.LastSnapshot = snapshotVersion ?? "Nothing";

        var releaseVersion = json["latest"]?["release"]?.ToString();
        if (_ShouldHintNewVersion(
                Config.Tool.ReleaseNotification,
                States.Tool.LastRelease,
                releaseVersion ?? ""))
        {
            _dlClientListMojangMainIsHinted = true;
            DownloadUserInteraction.Current?.ShowClientUpdateHint(releaseVersion ?? "", json);
        }

        States.Tool.LastRelease = releaseVersion ?? "";
    }

    private static bool _ShouldHintNewVersion(
        bool notificationEnabled,
        string lastSeenVersion,
        string currentVersion)
    {
        return notificationEnabled
            && lastSeenVersion != ""
            && lastSeenVersion != currentVersion
            && !_dlClientListMojangMainIsHinted;
    }

    private static void DlClientListBmclapiMain(
        Loading.LoaderTask<string, DlClientListResult> loader)
    {
        var json = (JsonObject)Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json");
        try
        {
            var versions = json["versions"] as JsonArray ?? [];
            _ValidateVersionList(versions, "BMCLAPI", json);
            _MergeUnlistedVersions(versions, "镜像源");
            _EnsureBmclapiHasInputVersion(loader);

            loader.output = new DlClientListResult
            {
                IsOfficial = false,
                SourceName = "BMCLAPI",
                Value = json
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "BMCLAPI", json),
                ex);
        }
    }

    private static void _EnsureBmclapiHasInputVersion(
        Loading.LoaderTask<string, DlClientListResult> loader)
    {
        if (string.IsNullOrEmpty(loader.input))
            return;

        var id = loader.input;
        if (dlClientListLoader.output is var output
            && (output.Value["versions"]?.AsArray() ?? [])
            .OfType<JsonObject>()
            .All(v => v["id"]?.ToString() != id))
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.BmclapiMissingTargetVersion", id));
    }

    /// <summary>
    ///     获取某个版本的 Json 下载地址，若失败则返回 Nothing。必须在工作线程执行。
    /// </summary>
    public static object? DlClientListGet(string id)
    {
        try
        {
            id = _NormalizeClientVersionId(id);
            switch (dlClientListLoader.State)
            {
                case LoadState.Finished:
                {
                    var cachedUrl = _FindClientVersionUrl(id);
                    if (cachedUrl is not null)
                        return cachedUrl;
                    dlClientListLoader.WaitForExit(id, isForceRestart: true);
                    break;
                }
                case LoadState.Loading:
                {
                    dlClientListLoader.WaitForExit(id);
                    break;
                }
                case LoadState.Failed
                    or LoadState.Aborted
                    or LoadState.Waiting:
                {
                    dlClientListLoader.WaitForExit(id, isForceRestart: true);
                    break;
                }
            }

            var versionUrl = _FindClientVersionUrl(id);
            if (versionUrl is not null)
                return versionUrl;

            LauncherCommon.Log(
                $"未发现版本 {id} 的 json 下载地址，版本列表返回为：\r\n{dlClientListLoader.output.Value}",
                LauncherLogLevel.Debug);
            return null;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, $"获取版本 {id} 的 json 下载地址失败");
            return null;
        }
    }

    private static string _NormalizeClientVersionId(string id)
    {
        id = id.Replace("_", "-");
        if (id != "1.0" && id.EndsWithF(".0"))
            id = id[..^2]; // OptiFine 1.8 的下载会触发此问题，显示版本为 1.8.0
        return id;
    }

    private static string? _FindClientVersionUrl(string id)
    {
        return dlClientListLoader.output is var output
            ? (output.Value["versions"]?.AsArray() ?? [])
            .OfType<JsonObject>()
            .Where(version => version["id"]?.ToString() == id)
            .Select(version => version["url"]?.ToString())
            .FirstOrDefault()
            : null;
    }

    public struct DlClientListResult
    {
        /// <summary>
        ///     数据来源名称，如“Mojang”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的 Json 数据。
        /// </summary>
        public JsonObject Value;
    }
}