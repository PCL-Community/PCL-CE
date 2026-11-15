using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

public static partial class GameDownload
{
    /// <summary>
    ///     Fabric 列表，主加载器。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlFabricListResult> DlFabricListLoader =
        new("DlFabricList Main", DlFabricListMain);

    /// <summary>
    ///     Fabric 列表，官方源。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlFabricListResult> DlFabricListOfficialLoader =
        new("DlFabricList Official", DlFabricListOfficialMain);

    /// <summary>
    ///     Fabric 列表，BMCLAPI。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlFabricListResult> DlFabricListBmclapiLoader =
        new("DlFabricList Bmclapi", DlFabricListBmclapiMain);

    private static void DlFabricListMain(Loading.LoaderTask<int, DlFabricListResult> loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlFabricListResult>, int>(
                            DlFabricListBmclapiLoader,
                            30),
                        new KeyValuePair<Loading.LoaderTask<int, DlFabricListResult>, int>(
                            DlFabricListOfficialLoader,
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
                        new KeyValuePair<Loading.LoaderTask<int, DlFabricListResult>, int>(
                            DlFabricListOfficialLoader,
                            5),
                        new KeyValuePair<Loading.LoaderTask<int, DlFabricListResult>, int>(
                            DlFabricListBmclapiLoader,
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
                        new KeyValuePair<Loading.LoaderTask<int, DlFabricListResult>, int>(
                            DlFabricListOfficialLoader,
                            60),
                        new KeyValuePair<Loading.LoaderTask<int, DlFabricListResult>, int>(
                            DlFabricListBmclapiLoader,
                            60 + 60)
                    ],
                    loader.isForceRestarting);
                break;
            }
        }
    }

    private static void DlFabricListOfficialMain(Loading.LoaderTask<int, DlFabricListResult> loader)
    {
        var result = (JsonObject)Requester.FetchJson("https://meta.fabricmc.net/v2/versions");
        try
        {
            var output = new DlFabricListResult
            {
                isOfficial = true,
                sourceName = Lang.Text("Download.Source.FabricOfficial"),
                Value = result
            };
            if (output.Value["game"] is null
                || output.Value["loader"] is null
                || output.Value["installer"] is null)
                throw new Exception(
                    Lang.Text(
                        "Minecraft.Download.Error.VersionListOperationFailed",
                        "Fabric",
                        result));
            loader.output = output;
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Fabric", result),
                ex);
        }
    }

    private static void DlFabricListBmclapiMain(Loading.LoaderTask<int, DlFabricListResult> loader)
    {
        var result = (JsonObject)Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions");
        try
        {
            var output = new DlFabricListResult
            {
                isOfficial = false,
                sourceName = "BMCLAPI",
                Value = result
            };
            if (output.Value["game"] is null
                || output.Value["loader"] is null
                || output.Value["installer"] is null)
                throw new Exception(
                    Lang.Text(
                        "Minecraft.Download.Error.VersionListOperationFailed",
                        "Fabric BMCLAPI",
                        result));
            loader.output = output;
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Fabric BMCLAPI",
                    result),
                ex);
        }
    }

    public struct DlFabricListResult
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
        public JsonObject Value;
    }
}