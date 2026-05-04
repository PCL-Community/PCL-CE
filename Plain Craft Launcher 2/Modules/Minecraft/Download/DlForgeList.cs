using System.Text;
using PCL.Core.Minecraft.Download;
using PCL.Network;

namespace PCL;

public class DlForgeList
{
    /// DlForgeList | Forge Minecraft 版本列表

    public struct DlForgeListResult
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
        public List<string> Value;
    }

    /// <summary>
    ///     Forge 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListLoader =
        new("DlForgeList Main", DlForgeListMain);

    private static void DlForgeListMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
    {
        DlSource.DlSourceLoader(Loader,
            DlSource.DlSourceVersionListGet(DlForgeListOfficialLoader, DlForgeListBmclapiLoader),
            Loader.IsForceRestarting);
    }

    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListOfficialLoader =
        new("DlForgeList Official",
            l => l.Output = FetchForgeList(
                DownloadRegistry.ForgeKnownVersions, "Forge 官方源", true,
                "(?<=a href=\"index_)[0-9.]+(_pre[0-9]?)?(?=.html)", "1.2.4",
                new RequestParam { Encoding = Encoding.Default, UseBrowserUserAgent = true }));

    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListBmclapiLoader =
        new("DlForgeList Bmclapi",
            l => l.Output = FetchForgeList(
                DownloadProvider.VersionList.ToBmclapiUrl(DownloadRegistry.ForgeKnownVersions), "BMCLAPI", false,
                "[0-9.]+(_pre[0-9]?)?", null,
                new RequestParam { Encoding = Encoding.Default }));

    private static DlForgeListResult FetchForgeList(string url, string sourceName, bool isOfficial,
        string versionRegex, string missingVersion, RequestParam param)
    {
        var result = Requester.FetchJson(url, param)?.ToString() ?? "";
        if (result.Length < 200)
            throw new Exception("获取到的版本列表长度不足（" + result + "）");
        var names = result.RegexSearch(versionRegex);
        if (missingVersion is not null)
            names.Add(missingVersion);
        if (names.Count < 10)
            throw new Exception("获取到的版本数量不足（" + result + "）");
        return new DlForgeListResult { IsOfficial = isOfficial, SourceName = sourceName, Value = names };
    }
}