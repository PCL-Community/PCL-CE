using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Network;

namespace PCL;

public class DlLegacyFabricList
{
        #region DlLegacyFabricList | LegacyFabric 列表

    public struct DlLegacyFabricListResult
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
    ///     LegacyFabric 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLegacyFabricListResult> DlLegacyFabricListLoader =
        new("DlLegacyFabricList Main", DlLegacyFabricListMain);

    private static void DlLegacyFabricListMain(ModLoader.LoaderTask<int, DlLegacyFabricListResult> Loader)
    {
        var timeout = Config.Download.VersionListSource switch { 0 => 30, 1 => 5, _ => 60 };
        DlSource.DlSourceLoader(Loader,
            [new KeyValuePair<ModLoader.LoaderTask<int, DlLegacyFabricListResult>, int>(DlLegacyFabricListOfficialLoader, timeout)],
            Loader.IsForceRestarting);
    }

    /// <summary>
    ///     LegacyFabric 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLegacyFabricListResult> DlLegacyFabricListOfficialLoader =
        new("DlLegacyFabricList Official", DlLegacyFabricListOfficialMain);

    private static void DlLegacyFabricListOfficialMain(ModLoader.LoaderTask<int, DlLegacyFabricListResult> Loader)
    {
        var Result =
            (JObject)Requester.FetchJson("https://meta.legacyfabric.net/v2/versions");
        try
        {
            var Output = new DlLegacyFabricListResult
                { IsOfficial = true, SourceName = "LegacyFabric 官方源", Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception("获取到的列表缺乏必要项");
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception("LegacyFabric 官方源版本列表解析失败（" + Result + "）", ex);
        }
    }

    /// <summary>
    ///     Legacy Fabric API 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlLegacyFabricApiLoader =
        new("Legacy Fabric API List Loader", Task => Task.Output = ModComp.CompFilesGet("legacy-fabric-api", false));

    #endregion
}