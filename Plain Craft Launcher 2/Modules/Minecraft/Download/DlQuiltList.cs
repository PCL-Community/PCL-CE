using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Network;

namespace PCL;

public class DlQuiltList
{
        #region DlQuiltList | Quilt 列表

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
        var Result = (JObject)Requester.FetchJson("https://meta.quiltmc.org/v3/versions");
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

    // ''' <summary>
    // ''' TODO: Quilt 列表，BMCLAPI。
    // ''' </summary>
    // Public DlQuiltListBmclapiLoader As New LoaderTask(Of Integer, DlQuiltListResult)("DlQuiltList Bmclapi", AddressOf DlQuiltListBmclapiMain)
    // Private Sub DlQuiltListBmclapiMain(Loader As LoaderTask(Of Integer, DlQuiltListResult))
    // Dim Result As JObject = NetGetCodeByRequestRetry("https://bmclapi2.bangbang93.com/Quilt-meta/v2/versions", IsJson:=True)
    // Try
    // Dim Output = New DlQuiltListResult With {.IsOfficial = False, .SourceName = "BMCLAPI", .Value = Result}
    // If Output.Value("game") Is Nothing OrElse Output.Value("loader") Is Nothing OrElse Output.Value("installer") Is Nothing Then Throw New Exception("获取到的列表缺乏必要项")
    // Loader.Output = Output
    // Catch ex As Exception
    // Throw New Exception("Quilt BMCLAPI 版本列表解析失败（" & Result.ToString & "）", ex)
    // End Try
    // End Sub

    /// <summary>
    ///     QSL 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, List<ModComp.CompFile>> DlQSLLoader = new("QSL List Loader",
        Task => Task.Output = ModComp.CompFilesGet("qsl", false));

    #endregion
}