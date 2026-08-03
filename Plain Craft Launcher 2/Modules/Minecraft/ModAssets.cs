using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using PCL;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL
{
    public static class ModAssets
    {
        // 获取索引
        /// <summary>
        ///     获取某实例资源文件索引的对应 Json 项，详见实例 Json 中的 assetIndex 项。失败会抛出异常。
        /// </summary>
        public static JsonNode McAssetsGetIndex(McInstance mcInstance, bool returnLegacyOnError = false,
            bool checkURLEmpty = false)
        {
            string assetsName;
            try
            {
                while (true)
                {
                    var index = mcInstance.JsonObject["assetIndex"];
                    if (index is not null && index["id"] is not null)
                        return index;
                    if (mcInstance.JsonObject["assets"] is not null)
                        assetsName = mcInstance.JsonObject["assets"].ToString();
                    if (checkURLEmpty && index["url"] is not null)
                        return index;
                    // 下一个实例
                    if (string.IsNullOrEmpty(mcInstance.InheritInstanceName))
                        break;
                    mcInstance = new McInstance(Path.Combine(ModFolder.mcFolderSelected, "versions", mcInstance.InheritInstanceName));
                }
            }
            catch
            {
            }

            // 无法获取到下载地址
            if (returnLegacyOnError)
            {
                // 返回 assets 文件名会由于没有下载地址导致全局失败
                // If AssetsName IsNot Nothing AndAlso AssetsName <> "legacy" Then
                // Log("[Minecraft] 无法获取资源文件索引下载地址，使用 assets 项提供的资源文件名：" & AssetsName)
                // Return GetJson("{""id"": """ & AssetsName & """}")
                // Else
                ModBase.Log("[Minecraft] 无法获取资源文件索引下载地址，使用默认的 legacy 下载地址");
                return (JsonNode)ModBase.GetJson(@"{
                    ""id"": ""legacy"",
                    ""sha1"": ""c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729"",
                    ""size"": 134284,
                    ""url"": ""https://launchermeta.mojang.com/mc-staging/assets/legacy/c0fd82e8ce9fbc93119e40d96d5a4e62cfa3f729/legacy.json"",
                    ""totalSize"": 111220701
                }");
            }
            // End If

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
                    if (mcInstance.JsonObject["assetIndex"] is not null &&
                        mcInstance.JsonObject["assetIndex"]["id"] is not null)
                        return mcInstance.JsonObject["assetIndex"]["id"].ToString();
                    if (mcInstance.JsonObject["assets"] is not null) return mcInstance.JsonObject["assets"].ToString();
                    if (string.IsNullOrEmpty(mcInstance.InheritInstanceName))
                        break;
                    mcInstance = new McInstance(Path.Combine(ModFolder.mcFolderSelected, "versions", mcInstance.InheritInstanceName));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取资源文件索引名失败");
            }

            return "legacy";
        }

        // 获取列表
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
                return ModBase.GetString(size) + " | " + localPath;
            }
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
                // 初始化
                if (!File.Exists($@"{ModFolder.mcFolderSelected}assets\indexes\{indexName}.json"))
                    throw new FileNotFoundException(Lang.Text("Minecraft.Error.AssetIndexNotFound"),
                        Path.Combine(ModFolder.mcFolderSelected, "assets", "indexes", indexName + ".json"));
                var result = new List<McAssetsToken>();
                var json = (JsonObject)ModBase.GetJson(
                    ModBase.ReadFile($@"{ModFolder.mcFolderSelected}assets\indexes\{indexName}.json"));

                // 读取列表
                foreach (var file in json["objects"].AsObject())
                {
                    string localPath;
                    var hash = file.Value["hash"].ToString();
                    if (json["map_to_resources"] is not null && json["map_to_resources"].ToObject<bool>())
                        // Remap
                        localPath = Path.Combine(mcInstance.PathIndie, "resources", file.Key.Replace("/", @"\"));
                    else if (json["virtual"] is not null && json["virtual"].ToObject<bool>())
                        // Virtual
                        localPath = Path.Combine(ModFolder.mcFolderSelected, "assets", "virtual", "legacy", file.Key.Replace("/", @"\"));
                    else
                    {
                        // 正常
                        localPath = Path.Combine(ModFolder.mcFolderSelected, "assets", "objects", McAssetsHashPrefix(hash), hash);
                    }
                    result.Add(new McAssetsToken
                    {
                        localPath = localPath,
                        sourcePath = file.Key,
                        hash = hash,
                        size = long.Parse(file.Value["size"].ToString())
                    });
                }

                return result;
            }

            catch (Exception ex)
            {
                ModBase.Log(ex, "获取资源文件列表失败：" + indexName);
                throw;
            }
        }

        // 获取缺失列表
        /// <summary>
        ///     获取实例缺失的资源文件所对应的 NetTaskFile。
        /// </summary>
        public static List<DownloadFile> McAssetsFixList(McInstance mcInstance, bool checkHash,
            [Optional] ref ModLoader.LoaderBase progressFeed)
        {
            // 如果需要检查 Hash，则留到下载时处理，以借助多线程加快检查速度
            if (checkHash)
                return McAssetsListGet(mcInstance).Select(token =>
                {
                    var hash = token.hash;
                    return new DownloadFile(
                        ModDownload.DlSourceAssetsGet(McAssetsUrl(hash)),
                        token.localPath,
                        new ModBase.FileChecker(actualSize: token.size == 0L ? -1 : token.size, hash: hash));
                }).ToList();
            // 如果不检查 Hash，则立即处理
            var result = new List<DownloadFile>();

            List<McAssetsToken> assetsList;
            try
            {
                assetsList = McAssetsListGet(mcInstance);
                McAssetsToken token;
                if (progressFeed is not null)
                    progressFeed.Progress = 0.04d;
                for (int i = 0, loopTo = assetsList.Count - 1; i <= loopTo; i++)
                {
                    // 初始化
                    token = assetsList[i];
                    if (progressFeed is not null)
                        progressFeed.Progress = 0.05d + 0.94d * i / assetsList.Count;
                    // 检查文件是否存在
                    var file = new FileInfo(token.localPath);
                    if (file.Exists && (token.size == 0L || token.size == file.Length))
                        continue;
                    // 文件不存在，添加下载
                    var hash = token.hash;
                    result.Add(new DownloadFile(
                        ModDownload.DlSourceAssetsGet(McAssetsUrl(hash)),
                        token.localPath,
                        new ModBase.FileChecker(actualSize: token.size == 0L ? -1 : token.size, hash: hash)));
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取实例缺失的资源文件下载列表失败");
            }

            if (progressFeed is not null)
                progressFeed.Progress = 0.99d;
            return result;
        }
    }
}
