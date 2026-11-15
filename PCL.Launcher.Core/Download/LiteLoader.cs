using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static partial class GameDownload
{
    /// <summary>
    ///     LiteLoader 版本列表，主加载器。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlLiteLoaderListResult> DlLiteLoaderListLoader =
        new("DlLiteLoaderList Main", DlLiteLoaderListMain);

    /// <summary>
    ///     LiteLoader 版本列表，官方源。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlLiteLoaderListResult>
        DlLiteLoaderListOfficialLoader =
            new("DlLiteLoaderList Official", DlLiteLoaderListOfficialMain);

    /// <summary>
    ///     LiteLoader 版本列表，BMCLAPI。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlLiteLoaderListResult>
        DlLiteLoaderListBmclapiLoader =
            new("DlLiteLoaderList Bmclapi", DlLiteLoaderListBmclapiMain);

    private static void DlLiteLoaderListMain(Loading.LoaderTask<int, DlLiteLoaderListResult> loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlLiteLoaderListResult>, int>(
                            DlLiteLoaderListBmclapiLoader,
                            30),
                        new KeyValuePair<Loading.LoaderTask<int, DlLiteLoaderListResult>, int>(
                            DlLiteLoaderListOfficialLoader,
                            30 + 60)
                    ],
                    loader.isForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlLiteLoaderListResult>, int>(
                            DlLiteLoaderListOfficialLoader,
                            5),
                        new KeyValuePair<Loading.LoaderTask<int, DlLiteLoaderListResult>, int>(
                            DlLiteLoaderListBmclapiLoader,
                            5 + 30)
                    ],
                    loader.isForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlLiteLoaderListResult>, int>(
                            DlLiteLoaderListOfficialLoader,
                            60),
                        new KeyValuePair<Loading.LoaderTask<int, DlLiteLoaderListResult>, int>(
                            DlLiteLoaderListBmclapiLoader,
                            60 + 60)
                    ],
                    loader.isForceRestarting);
                break;
            }
        }
    }

    private static void DlLiteLoaderListOfficialMain(
        Loading.LoaderTask<int, DlLiteLoaderListResult> loader)
    {
        var result =
            (JsonObject)Requester.FetchJson("https://dl.liteloader.com/versions/versions.json");
        try
        {
            var json = result["versions"] as JsonObject ?? [];
            var versions = new List<DlLiteLoaderListEntry>();
            foreach (var (key, value) in json)
            {
                if (key.StartsWithF("1.6") || key.StartsWithF("1.5")) continue;
                var realEntry =
                    (value?["artefacts"] ?? value?["snapshots"])?["com.mumfrey:liteloader"]?[
                        "latest"];
                if (realEntry is null) continue;
                versions.Add(
                    new DlLiteLoaderListEntry
                    {
                        Inherit = key,
                        IsLegacy = double.Parse(key.Split(".")[1]) < 8d,
                        IsPreview = realEntry["stream"]?.ToString()?.ToLower() == "snapshot",
                        FileName =
                            $"liteloader-installer-{key}{(key is "1.8" or "1.9" ? ".0" : "")}-00-SNAPSHOT.jar",
                        Md5 = (string?)realEntry["md5"],
                        ReleaseTime = TimeUtils
                            .FromUnixTimestamp(
                                long.Parse(realEntry["timestamp"]?.ToString() ?? "0"))
                            .LocalDateTime,
                        jsonToken = realEntry
                    });
            }

            loader.output = new DlLiteLoaderListResult
            {
                isOfficial = true,
                sourceName = Lang.Text("Download.Source.LiteLoaderOfficial"),
                Value = versions
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "LiteLoader",
                    result),
                ex);
        }
    }

    private static void DlLiteLoaderListBmclapiMain(
        Loading.LoaderTask<int, DlLiteLoaderListResult> loader)
    {
        var result =
            (JsonObject)Requester.FetchJson(
                "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json");
        try
        {
            var json = result["versions"] as JsonObject ?? [];
            var versions = new List<DlLiteLoaderListEntry>();
            foreach (var (key, value) in json)
            {
                if (key.StartsWithF("1.6") || key.StartsWithF("1.5")) continue;
                var realEntry =
                    (value?["artefacts"] ?? value?["snapshots"])?["com.mumfrey:liteloader"]?[
                        "latest"];
                if (realEntry is null) continue;
                versions.Add(
                    new DlLiteLoaderListEntry
                    {
                        Inherit = key,
                        IsLegacy = double.Parse(key.Split(".")[1]) < 8d,
                        IsPreview = realEntry["stream"]?.ToString()?.ToLower() == "snapshot",
                        FileName =
                            $"liteloader-installer-{key}{(key is "1.8" or "1.9" ? ".0" : "")}-00-SNAPSHOT.jar",
                        Md5 = (string?)realEntry["md5"],
                        ReleaseTime = TimeUtils
                            .FromUnixTimestamp(
                                long.Parse(realEntry["timestamp"]?.ToString() ?? "0"))
                            .LocalDateTime,
                        jsonToken = realEntry
                    });
            }


            loader.output = new DlLiteLoaderListResult
            {
                isOfficial = false,
                sourceName = "BMCLAPI",
                Value = versions
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "LiteLoader BMCLAPI",
                    result),
                ex);
        }
    }

    public struct DlLiteLoaderListResult
    {
        /// <summary>
        ///     数据来源名称，如“Official”，“BMCLAPI”。
        /// </summary>
        public string sourceName;

        /// <summary>
        ///     是否为官方的实时数据。
        /// </summary>
        public bool isOfficial;

        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public List<DlLiteLoaderListEntry> Value;

        /// <summary>
        ///     官方源的失败原因。若没有则为 Nothing。
        /// </summary>
        public Exception officialError;
    }

    public class DlLiteLoaderListEntry(
        string? fileName = null,
        string? inherit = null,
        bool isLegacy = false,
        bool isPreview = false,
        string? md5 = null,
        DateTime? releaseTime = null,
        JsonNode? jsonToken = null)
    {
        /// <summary>
        ///     实际的文件名，如“liteloader-installer-1.12-00-SNAPSHOT.jar”。
        /// </summary>
        public string? FileName = fileName;

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string? Inherit = inherit;

        /// <summary>
        ///     是否为 1.7 及更早的远古版。
        /// </summary>
        public bool IsLegacy = isLegacy;

        /// <summary>
        ///     是否为测试版。
        /// </summary>
        public bool IsPreview = isPreview;

        /// <summary>
        ///     文件的 MD5。
        /// </summary>
        public string? Md5 = md5;

        /// <summary>
        ///     发布时间。<br />
        ///     该字段保存原始时间，展示时请使用 <see cref="Lang.Date" /> 按当前区域性格式化。
        /// </summary>
        public DateTime? ReleaseTime = releaseTime;

        /// <summary>
        ///     对应的 Json 项。
        /// </summary>
        public JsonNode? jsonToken = jsonToken;
    }
}