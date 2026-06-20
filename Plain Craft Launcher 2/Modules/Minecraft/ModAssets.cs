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
            var resolution = LauncherAssetsApplicationAdapter.ResolveIndex(mcInstance, returnLegacyOnError, checkURLEmpty);
            if (resolution.IndexJson is not null)
            {
                if (resolution.UsedLegacyFallback)
                    ModBase.Log("[Minecraft] 无法获取资源文件索引下载地址，使用默认的 legacy 下载地址");
                return resolution.IndexJson;
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
                return LauncherAssetsApplicationAdapter.GetIndexName(mcInstance);
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
            return LauncherAssetsApplicationAdapter.GetAssetHashPrefix(hash);
        }

        internal static string McAssetsUrl(string hash)
        {
            return LauncherAssetsApplicationAdapter.GetAssetObjectUrl(hash);
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
                var json = (JsonObject)ModBase.GetJson(
                    ModBase.ReadFile($@"{ModFolder.mcFolderSelected}assets\indexes\{indexName}.json"));
                return LauncherAssetsApplicationAdapter.GetAssetList(mcInstance, json);
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
                return ToDownloadFiles(LauncherAssetsApplicationAdapter.CreateAssetDownloadPlan(
                    McAssetsListGet(mcInstance),
                    checkHash: true,
                    new Dictionary<string, LauncherAssetFileState>()));
            // 如果不检查 Hash，则立即处理
            List<McAssetsToken> assetsList;
            try
            {
                assetsList = McAssetsListGet(mcInstance);
                Dictionary<string, LauncherAssetFileState> existingFiles = [];
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
                    existingFiles[token.localPath] = new LauncherAssetFileState(file.Exists, file.Exists ? file.Length : 0);
                }

                if (progressFeed is not null)
                    progressFeed.Progress = 0.99d;
                return ToDownloadFiles(LauncherAssetsApplicationAdapter.CreateAssetDownloadPlan(
                    assetsList,
                    checkHash: false,
                    existingFiles));
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "获取实例缺失的资源文件下载列表失败");
            }

            if (progressFeed is not null)
                progressFeed.Progress = 0.99d;
            return [];
        }

        private static List<DownloadFile> ToDownloadFiles(LauncherAssetDownloadPlan plan)
        {
            return plan.Files.Select(static file => new DownloadFile(
                ModDownload.DlSourceAssetsGet(file.Url),
                file.LocalPath,
                new ModBase.FileChecker(actualSize: file.ActualSize, hash: file.Hash))).ToList();
        }
    }
}
