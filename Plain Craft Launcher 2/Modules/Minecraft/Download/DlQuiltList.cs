using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.Minecraft.Download;
using PCL.Network;

namespace PCL;

public class DlQuiltList
{
    /// DlQuiltList | Quilt 列表

    public struct DlQuiltListResult
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
    ///     Quilt 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlQuiltListResult> DlQuiltListLoader =
        new("DlQuiltList Main", DlQuiltListMain);

    private static void DlQuiltListMain(ModLoader.LoaderTask<int, DlQuiltListResult> Loader)
    {
        var firstTimeout = Config.Download.VersionListSource switch { 0 => 30, 1 => 5, _ => 60 };
        var secondTimeout = Config.Download.VersionListSource switch { 0 => 60, 1 => 35, _ => 60 };
        DlSource.DlSourceLoader(Loader,
            [
                new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, firstTimeout),
                new KeyValuePair<ModLoader.LoaderTask<int, DlQuiltListResult>, int>(DlQuiltListOfficialLoader, secondTimeout)
            ],
            Loader.IsForceRestarting);
    }

    /// <summary>
    ///     Quilt 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlQuiltListResult> DlQuiltListOfficialLoader =
        new("DlQuiltList Official", DlQuiltListOfficialMain);

    private static void DlQuiltListOfficialMain(ModLoader.LoaderTask<int, DlQuiltListResult> Loader)
    {
        var Result = (JObject)Requester.FetchJson(DownloadRegistry.QuiltMeta);
        try
        {
            var Output = new DlQuiltListResult { IsOfficial = true, SourceName = "Quilt 官方源", Value = Result };
            if (Output.Value["game"] is null || Output.Value["loader"] is null || Output.Value["installer"] is null)
                throw new Exception("获取到的列表缺乏必要项");
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception("Quilt 官方源版本列表解析失败（" + Result + "）", ex);
        }
    }

    /// <summary>
    ///     QSL 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlQSLLoader = new("QSL List Loader",
        Task => Task.Output = ModComp.CompFilesGet("qsl", false));
}