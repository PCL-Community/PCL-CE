using Newtonsoft.Json.Linq;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public class DlLiteLoaderList
{
    /// DlLiteLoaderList | LiteLoader 版本列表

    public struct DlLiteLoaderListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string SourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool IsOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public List<DlLiteLoaderListEntry> Value;
    }

    public class DlLiteLoaderListEntry
    {
        /// <summary>
        ///     实际的文件名，如“liteloader-installer-1.12-00-SNAPSHOT.jar”。
        /// </summary>
        public string FileName;

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string Inherit;

        /// <summary>
        ///     是否为 1.7 及更早的远古版。
        /// </summary>
        public bool IsLegacy;

        /// <summary>
        ///     是否为测试版。
        /// </summary>
        public bool IsPreview;

        /// <summary>
        ///     对应的 Json 项。
        /// </summary>
        public JToken JsonToken;

        /// <summary>
        ///     文件的 MD5。
        /// </summary>
        public string MD5;

        /// <summary>
        ///     发布时间，格式为“yyyy/mm/dd HH:mm”。
        /// </summary>
        public string ReleaseTime;
    }

    /// <summary>
    ///     LiteLoader 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListLoader =
        new("DlLiteLoaderList Main", DlLiteLoaderListMain);

    private static void DlLiteLoaderListMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
    {
        DlSource.DlSourceLoader(Loader,
            DlSource.DlSourceVersionListGet(DlLiteLoaderListOfficialLoader, DlLiteLoaderListBmclapiLoader),
            Loader.IsForceRestarting);
    }

    /// <summary>
    ///     LiteLoader 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListOfficialLoader =
        new("DlLiteLoaderList Official", DlLiteLoaderListOfficialMain);

    private static void DlLiteLoaderListOfficialMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
    {
        var Result =
            (JObject)Requester.FetchJson("https://dl.liteloader.com/versions/versions.json");
        try
        {
            Loader.Output = new DlLiteLoaderListResult
                { IsOfficial = true, SourceName = "LiteLoader 官方源", Value = ParseLiteLoaderVersions((JObject)Result["versions"]) };
        }
        catch (Exception ex)
        {
            throw new Exception("LiteLoader 官方源版本列表解析失败（" + Result + "）", ex);
        }
    }

    /// <summary>
    ///     LiteLoader 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListBmclapiLoader =
        new("DlLiteLoaderList Bmclapi", DlLiteLoaderListBmclapiMain);

    private static void DlLiteLoaderListBmclapiMain(ModLoader.LoaderTask<int, DlLiteLoaderListResult> Loader)
    {
        var Result =
            (JObject)Requester.FetchJson(
                "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json");
        try
        {
            Loader.Output = new DlLiteLoaderListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = ParseLiteLoaderVersions((JObject)Result["versions"]) };
        }
        catch (Exception ex)
        {
            throw new Exception("LiteLoader BMCLAPI 版本列表解析失败（" + Result + "）", ex);
        }
    }

    private static List<DlLiteLoaderListEntry> ParseLiteLoaderVersions(JObject versions)
    {
        var entries = new List<DlLiteLoaderListEntry>();
        foreach (var Pair in versions)
        {
            if (Pair.Key.StartsWithF("1.6") || Pair.Key.StartsWithF("1.5"))
                continue;
            var RealEntry =
                (Pair.Value["artefacts"] ?? Pair.Value["snapshots"])["com.mumfrey:liteloader"]["latest"];
            entries.Add(new DlLiteLoaderListEntry
            {
                Inherit = Pair.Key,
                IsLegacy = double.Parse(Pair.Key.Split(".")[1]) < 8,
                IsPreview = RealEntry["stream"].ToString().ToLower() == "snapshot",
                FileName = "liteloader-installer-" + Pair.Key +
                           (Pair.Key == "1.8" || Pair.Key == "1.9" ? ".0" : "") + "-00-SNAPSHOT.jar",
                MD5 = (string)RealEntry["md5"],
                ReleaseTime = TimeUtils.FormatUnixTimestamp((long)RealEntry["timestamp"]),
                JsonToken = RealEntry
            });
        }
        return entries;
    }
}