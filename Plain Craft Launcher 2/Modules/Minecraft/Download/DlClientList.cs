using System.IO;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public class DlClientList
{
    /// DlClientList | Minecraft 客户端 版本列表

    /// <summary>
    ///     所有正式版的 Minecraft Drop 序数。
    ///     若从未完成过获取，返回 Nothing；否则必定存在元素，且从高到低排列。
    /// </summary>
    public static List<int> AllDrops
    {
        get
        {
            lock (_allDropsLock)
            {
                if (_allDrops is null)
                {
                    var rawData = States.Game.Drops;
                    if (string.IsNullOrEmpty(rawData))
                        _allDrops = new List<int>();
                    else
                        _allDrops = rawData.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(d => (int)Math.Round(ModBase.Val(d))).ToList();
                }

                return _allDrops.Count != 0 ? _allDrops : null;
            }
        }
        set
        {
            lock (_allDropsLock)
            {
                _allDrops = value;
                States.Game.Drops = value.Join(",");
            }
        }
    }

    private static List<int> _allDrops;
    private static readonly object _allDropsLock = new();

    // 主加载器
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
        public JObject Value;
    }

    /// <summary>
    ///     Minecraft 客户端 版本列表，主加载器。
    ///     若要求镜像源必须包含某个版本，则将该版本 ID 作为输入（#5195）。
    /// </summary>
    public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListLoader =
        new("DlClientList Main", DlClientListMain);

    private static void DlClientListMain(ModLoader.LoaderTask<string, DlClientListResult> loader)
    {
        DlSource.DlSourceLoader(loader,
            DlSource.DlSourceVersionListGet(DlClientListMojangLoader, DlClientListBmclapiLoader),
            loader.IsForceRestarting);

        var drops = new List<int>();
        foreach (JObject version in loader.Output.Value["versions"])
            drops.Add(ModMinecraft.McInstanceInfo.VersionToDrop((string)version["id"]));
        AllDrops = drops.Distinct().OrderByDescending(d => d).ToList();
    }

    // 各个下载源的分加载器
    /// <summary>
    ///     Minecraft 客户端 版本列表，Mojang 官方源加载器。
    /// </summary>
    public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListMojangLoader =
        new("DlClientList Mojang", DlClientListMojangMain);

    private static bool _DlClientListMojangMain_IsHinted;

    private static void DlClientListMojangMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
    {
        var StartTime = TimeUtils.GetTimeTick();
        var Json = (JObject)Requester.FetchJson("https://launchermeta.mojang.com/mc/game/version_manifest.json");
        try
        {
            var Versions = (JArray)Json["versions"];
            if (Versions.Count < 200)
                throw new Exception("获取到的版本列表长度不足（" + Json + "）");
            MergeUvmcVersions(Versions, "官方源");

            // 确定官方源是否可用
            if (!DlSource.DlPreferMojang)
            {
                var DeltaTime = TimeUtils.GetTimeTick() - StartTime;
                DlSource.DlPreferMojang = DeltaTime < 4000;
                ModBase.Log($"[Download] Mojang 官方源加载耗时：{DeltaTime}ms，{(DlSource.DlPreferMojang ? "可优先使用官方源" : "不优先使用官方源")}");
            }

            // 添加 PCL 特供项
            // 这个社区版下不了
            // If File.Exists(PathTemp & "Cache\download.json") Then Versions.Merge(GetJson(ReadFile(PathTemp & "Cache\download.json")))
            // 返回
            Loader.Output = new DlClientListResult { IsOfficial = true, SourceName = "Mojang 官方源", Value = Json };
            string Version;
            // 快照版
            Version = (string)Json["latest"]["snapshot"];
            if (Conversions.ToBoolean((bool)Config.Tool.SnapshotNotification &&
                                      !Operators.ConditionalCompareObjectEqual(
                                          States.Tool.LastSnapshot, "", false) &&
                                      Operators.ConditionalCompareObjectNotEqual(
                                          States.Tool.LastSnapshot, Version, false) &&
                                      !_DlClientListMojangMain_IsHinted))
            {
                _DlClientListMojangMain_IsHinted = true;
                ModMinecraft.McDownloadClientUpdateHint(Version, Json);
            }

            States.Tool.LastSnapshot = Version ?? "Nothing";
            // 正式版
            Version = (string)Json["latest"]["release"];
            if (Conversions.ToBoolean((bool)Config.Tool.ReleaseNotification &&
                                      !Operators.ConditionalCompareObjectEqual(
                                          States.Tool.LastRelease, "", false) &&
                                      Operators.ConditionalCompareObjectNotEqual(
                                          States.Tool.LastRelease, Version, false) &&
                                      !_DlClientListMojangMain_IsHinted))
            {
                _DlClientListMojangMain_IsHinted = true;
                ModMinecraft.McDownloadClientUpdateHint(Version, Json);
            }

            States.Tool.LastRelease = Version;
        }
        catch (Exception ex)
        {
            throw new Exception("Minecraft 官方源版本列表解析失败", ex);
        }
    }

    /// <summary>
    ///     Minecraft 客户端 版本列表，BMCLAPI 源加载器。
    /// </summary>
    public static ModLoader.LoaderTask<string, DlClientListResult> DlClientListBmclapiLoader =
        new("DlClientList Bmclapi", DlClientListBmclapiMain);

    private static void DlClientListBmclapiMain(ModLoader.LoaderTask<string, DlClientListResult> Loader)
    {
        var Json = (JObject)Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json");
        try
        {
            var Versions = (JArray)Json["versions"];
            if (Versions.Count < 200)
                throw new Exception("获取到的版本列表长度不足（" + Json + "）");
            MergeUvmcVersions(Versions, "镜像源");

            // 检查是否有要求的版本（#5195）
            if (!string.IsNullOrEmpty(Loader.Input))
            {
                var Id = Loader.Input;
                if (DlClientListLoader.Output.Value is not null &&
                    !DlClientListLoader.Output.Value["versions"].Any(v => (string)v["id"] == Id))
                    throw new Exception("BMCLAPI 源未包含目标版本 " + Id);
            }

            // 返回
            Loader.Output = new DlClientListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Json };
        }
        catch (Exception ex)
        {
            throw new Exception("Minecraft BMCLAPI 版本列表解析失败（" + Json + "）", ex);
        }
    }

    private static void MergeUvmcVersions(JArray versions, string sourceDesc)
    {
        var CacheFilePath = ModBase.PathTemp + @"Cache\uvmc-download.json";
        if (!File.Exists(CacheFilePath))
            try
            {
                var UnlistedJson = (JObject)Requester.FetchJson(
                    "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto/version_manifest.json");
                File.WriteAllText(CacheFilePath, UnlistedJson.ToString());
            }
            catch (Exception ex)
            {
                ModBase.Log($"[Download] 未列出的版本{sourceDesc}下载失败: " + ex.Message);
            }

        try
        {
            var CachedJson = (JObject)ModBase.GetJson(ModBase.ReadFile(CacheFilePath));
            versions.Merge(CachedJson["versions"]);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Download] UVMC 列表加载失败，忽略列表内容");
        }
    }

    /// <summary>
    ///     获取某个版本的 Json 下载地址，若失败则返回 Nothing。必须在工作线程执行。
    /// </summary>
    public static object DlClientListGet(string Id)
    {
        try
        {
            // 确认版本格式标准
            Id = Id.Replace("_", "-"); // 1.7.10_pre4 在版本列表中显示为 1.7.10-pre4
            if (Id != "1.0" && Id.EndsWithF(".0"))
                Id = Strings.Left(Id, Id.Length - 2); // OptiFine 1.8 的下载会触发此问题，显示版本为 1.8.0
            // 获取 Minecraft 版本列表
            switch (DlClientListLoader.State)
            {
                case ModBase.LoadState.Finished:
                {
                    // 从当前的结果获取目标版本…
                    foreach (JObject Version in DlClientListLoader.Output.Value["versions"])
                        if ((string)Version["id"] == Id)
                            return Version["url"].ToString();
                    // …如果没有，则重新尝试获取（在版本刚更新时可能出现这种情况，#5195）
                    DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                    break;
                }
                case ModBase.LoadState.Loading:
                {
                    DlClientListLoader.WaitForExit(Id);
                    break;
                }
                case ModBase.LoadState.Failed:
                case ModBase.LoadState.Aborted:
                case ModBase.LoadState.Waiting:
                {
                    DlClientListLoader.WaitForExit(Id, IsForceRestart: true);
                    break;
                }
            }

            // 重新查找版本
            foreach (JObject Version in DlClientListLoader.Output.Value["versions"])
                if ((string)Version["id"] == Id)
                    return Version["url"].ToString();
            ModBase.Log($"未发现版本 {Id} 的 json 下载地址，版本列表返回为：{"\r\n"}{DlClientListLoader.Output.Value}",
                ModBase.LogLevel.Debug);
            return null;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"获取版本 {Id} 的 json 下载地址失败");
            return null;
        }
    }
}