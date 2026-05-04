using Newtonsoft.Json.Linq;
using PCL.Core.Minecraft.Download;
using PCL.Network;

namespace PCL;

public class DlFabricList
{
    /// DlFabricList | Fabric 列表

    public struct DlFabricListResult
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
        public JObject Value;
    }

    /// <summary>
    ///     Fabric 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListLoader =
        new("DlFabricList Main", DlFabricListMain);

    private static void DlFabricListMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
    {
        DlSource.DlSourceLoader(Loader,
            DlSource.DlSourceVersionListGet(DlFabricListOfficialLoader, DlFabricListBmclapiLoader),
            Loader.IsForceRestarting);
    }

    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListOfficialLoader =
        new("DlFabricList Official", l => l.Output = FetchFabricList(DownloadRegistry.FabricMeta, "Fabric 官方源", true));

    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListBmclapiLoader =
        new("DlFabricList Bmclapi",
            l => l.Output = FetchFabricList(
                DownloadProvider.VersionList.ToBmclapiUrl(DownloadRegistry.FabricMeta), "BMCLAPI", false));

    private static DlFabricListResult FetchFabricList(string url, string sourceName, bool isOfficial)
    {
        var result = (JObject)Requester.FetchJson(url);
        var output = new DlFabricListResult { IsOfficial = isOfficial, SourceName = sourceName, Value = result };
        if (output.Value["game"] is null || output.Value["loader"] is null || output.Value["installer"] is null)
            throw new Exception("获取到的列表缺乏必要项");
        return output;
    }

    /// <summary>
    ///     Fabric API 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlFabricApiLoader = new("Fabric API List Loader",
        Task => Task.Output = ModComp.CompFilesGet("fabric-api", false));

    /// <summary>
    ///     OptiFabric 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlOptiFabricLoader =
        new("OptiFabric List Loader", Task => Task.Output = ModComp.CompFilesGet("322385", true));
}