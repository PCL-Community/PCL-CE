using System.Runtime.InteropServices;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static class GameAssets
{
    /// <summary>
    ///     获取某实例资源文件索引的对应 Json 项，详见实例 Json 中的 assetIndex 项。失败会抛出异常。
    /// </summary>
    public static JsonNode McAssetsGetIndex(
        McInstance mcInstance,
        bool returnLegacyOnError = false,
        bool checkUrlEmpty = false)
    {
        try
        {
            while (true)
            {
                var index = mcInstance.JsonObject["assetIndex"];
                if (index?["id"] != null)
                    return index;

                _ = mcInstance.JsonObject["assets"]?.ToString();
                if (index is not null
                    && checkUrlEmpty
                    && index["url"] is not null)
                    return index;

                if (string.IsNullOrEmpty(mcInstance.InheritInstanceName)
                    || GameFolders.mcFolderSelected is null)
                    break;

                mcInstance = new McInstance(
                    Path.Combine(
                        GameFolders.mcFolderSelected,
                        "versions",
                        mcInstance.InheritInstanceName));
            }
        }
        catch
        {
            // ignored
        }

        if (returnLegacyOnError)
        {
            LauncherCommon.Log("[Minecraft] 无法获取资源文件索引下载地址，使用默认的 legacy 下载地址");
            return LauncherCommon.GetJson(
                """
                {
                  "id": "legacy",
                  "sha1": "c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729",
                  "size": 134284,
                  "url": "https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json",
                  "totalSize": 111220701
                }
                """);
        }

        throw new Exception(Lang.Text("Minecraft.Error.NoAssetIndexInfo"));
    }

    /// <summary>
    ///     获取某实例资源文件索引名，优先使用 assetIndex，其次使用 assets。失败会返回 legacy。
    /// </summary>
    public static string McAssetsGetIndexName(McInstance mcInstance)
    {
        try
        {
            while (true)
            {
                var indexId = mcInstance.JsonObject["assetIndex"]?["id"];
                if (indexId is not null)
                    return indexId.ToString();

                var assets = mcInstance.JsonObject["assets"];
                if (assets is not null)
                    return assets.ToString();

                if (string.IsNullOrEmpty(mcInstance.InheritInstanceName)
                    || GameFolders.mcFolderSelected is null)
                    break;

                mcInstance = new McInstance(
                    Path.Combine(
                        GameFolders.mcFolderSelected,
                        "versions",
                        mcInstance.InheritInstanceName));
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "获取资源文件索引名失败");
        }

        return "legacy";
    }

    internal static string McAssetsHashPrefix(string hash)
    {
        return hash[..2];
    }

    internal static string McAssetsUrl(string hash)
    {
        return $"https://resources.download.minecraft.net/{McAssetsHashPrefix(hash)}/{hash}";
    }

    /// <summary>
    ///     获取 Minecraft 的资源文件列表。失败会抛出异常。
    /// </summary>
    internal static List<McAssetsToken> McAssetsListGet(McInstance mcInstance)
    {
        var indexName = McAssetsGetIndexName(mcInstance);
        try
        {
            if (GameFolders.mcFolderSelected is null)
                throw new InvalidOperationException("尚未选择 Minecraft 文件夹");
            if (!File.Exists($@"{GameFolders.mcFolderSelected}assets\indexes\{indexName}.json"))
                throw new FileNotFoundException(
                    Lang.Text("Minecraft.Error.AssetIndexNotFound"),
                    Path.Combine(
                        GameFolders.mcFolderSelected,
                        "assets",
                        "indexes",
                        $"{indexName}.json"));
            var result = new List<McAssetsToken>();
            var json = (JsonObject)LauncherCommon.GetJson(
                LauncherCommon.ReadFile(
                    $@"{GameFolders.mcFolderSelected}assets\indexes\{indexName}.json"));

            var objects = json["objects"]?.AsObject()
                ?? throw new InvalidOperationException("资源索引缺少 objects 字段");
            foreach (var file in objects)
            {
                string localPath;
                var hash = file.Value?["hash"]?.ToString()
                    ?? throw new InvalidOperationException($"资源索引项缺少 hash 字段：{file.Key}");
                if (json["map_to_resources"]?.ToObject<bool>() == true)
                    localPath = Path.Combine(
                        mcInstance.PathIndie,
                        "resources",
                        file.Key.Replace("/", @"\"));
                else if (json["virtual"]?.ToObject<bool>() == true)
                    localPath = Path.Combine(
                        GameFolders.mcFolderSelected,
                        "assets",
                        "virtual",
                        "legacy",
                        file.Key.Replace("/", @"\"));
                else
                    localPath = Path.Combine(
                        GameFolders.mcFolderSelected,
                        "assets",
                        "objects",
                        McAssetsHashPrefix(hash),
                        hash);
                result.Add(
                    new McAssetsToken
                    {
                        localPath = localPath,
                        sourcePath = file.Key,
                        hash = hash,
                        size = long.Parse(
                            file.Value["size"]?.ToString()
                            ?? throw new InvalidOperationException(
                                $"资源索引项缺少 size 字段：{file.Key}"))
                    });
            }

            return result;
        }

        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "获取资源文件列表失败：" + indexName);
            throw;
        }
    }

    /// <summary>
    ///     获取实例缺失的资源文件所对应的 NetTaskFile。
    /// </summary>
    public static List<DownloadFile> McAssetsFixList(
        McInstance mcInstance,
        bool checkHash,
        [Optional] ref Loading.LoaderBase progressFeed)
    {
        if (checkHash)
            return
            [
                .. McAssetsListGet(mcInstance)
                    .Select(token =>
                    {
                        var hash = token.hash;
                        return new DownloadFile(
                            DownloadSources.AssetsGet(McAssetsUrl(hash)),
                            token.localPath,
                            new FileChecker(
                                actualSize: token.size == 0L ? -1 : token.size,
                                hash: hash));
                    })
            ];
        var result = new List<DownloadFile>();

        try
        {
            var assetsList = McAssetsListGet(mcInstance);
            if (progressFeed is not null)
                progressFeed.Progress = 0.04d;
            for (int i = 0, loopTo = assetsList.Count - 1; i <= loopTo; i++)
            {
                var token = assetsList[i];
                progressFeed?.Progress = 0.05d + 0.94d * i / assetsList.Count;

                var file = new FileInfo(token.localPath);
                if (file.Exists && (token.size == 0L || token.size == file.Length))
                    continue
                        ;
                var hash = token.hash;
                result.Add(
                    new DownloadFile(
                        DownloadSources.AssetsGet(McAssetsUrl(hash)),
                        token.localPath,
                        new FileChecker(
                            actualSize: token.size == 0L ? -1 : token.size,
                            hash: hash)));
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "获取实例缺失的资源文件下载列表失败");
        }

        progressFeed?.Progress = 0.99d;
        return result;
    }

    public struct McAssetsToken
    {
        /// <summary>
        ///     文件的完整本地路径。
        /// </summary>
        public string localPath;

        /// <summary>
        ///     Json 中书写的源路径。例如 minecraft/sounds/mob/stray/death2.ogg 。
        /// </summary>
        public string sourcePath;

        /// <summary>
        ///     文件大小。若无有效数据即为 0。
        /// </summary>
        public long size;

        /// <summary>
        ///     文件的 Hash 校验码。
        /// </summary>
        public string hash;

        public override string ToString()
        {
            return $"{LauncherCommon.GetString(size)} | {localPath}";
        }
    }
}