using Newtonsoft.Json.Linq;
using PCL.Network;

namespace PCL;

public class DlFabricList
{
        #region DlFabricList | Fabric 列表

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

    /// <summary>
    ///     Fabric 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListOfficialLoader =
        new("DlFabricList Official", DlFabricListOfficialMain);

    private static void DlFabricListOfficialMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
    {
        var Result = (JObject)Requester.FetchJson("https://meta.fabricmc.net/v2/versions");
        try
        {
            var Output = new DlFabricListResult { IsOfficial = true, SourceName = "Fabric 官方源", Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception("获取到的列表缺乏必要项");
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception("Fabric 官方源版本列表解析失败（" + Result + "）", ex);
        }
    }

    /// <summary>
    ///     Fabric 列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlFabricListResult> DlFabricListBmclapiLoader =
        new("DlFabricList Bmclapi", DlFabricListBmclapiMain);

    private static void DlFabricListBmclapiMain(ModLoader.LoaderTask<int, DlFabricListResult> Loader)
    {
        var Result = (JObject)Requester.FetchJson("https://bmclapi2.bangbang93.com/fabric-meta/v2/versions");
        try
        {
            var Output = new DlFabricListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception("获取到的列表缺乏必要项");
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception("Fabric BMCLAPI 版本列表解析失败（" + Result + "）", ex);
        }
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

    #endregion
}