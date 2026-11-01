using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Network;

namespace PCL;

public static partial class GameDownload
{
    /// <summary>
    ///     LegacyFabric 列表，主加载器。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlLegacyFabricListResult> DlLegacyFabricListLoader =
        new("DlLegacyFabricList Main", DlLegacyFabricListMain);

    /// <summary>
    ///     LegacyFabric 列表，官方源。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlLegacyFabricListResult>
        DlLegacyFabricListOfficialLoader =
            new("DlLegacyFabricList Official", DlLegacyFabricListOfficialMain);

    /// <summary>
    ///     Legacy Fabric API 列表，官方源。
    /// </summary>
    public static readonly Loading.LoaderTask<int, List<Comp.CompFile>> DlLegacyFabricApiLoader =
        new(
            "Legacy Fabric API List Loader",
            task => task.output = Comp.CompFilesGet("legacy-fabric-api", false));

    private static void DlLegacyFabricListMain(
        Loading.LoaderTask<int, DlLegacyFabricListResult> loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlLegacyFabricListResult>, int>(
                            DlLegacyFabricListOfficialLoader,
                            30)
                    ],
                    loader.isForceRestarting);
                break;
            }
            case 1:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlLegacyFabricListResult>, int>(
                            DlLegacyFabricListOfficialLoader,
                            5)
                    ],
                    loader.isForceRestarting);
                break;
            }

            default:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlLegacyFabricListResult>, int>(
                            DlLegacyFabricListOfficialLoader,
                            60)
                    ],
                    loader.isForceRestarting);
                break;
            }
        }
    }

    private static void DlLegacyFabricListOfficialMain(
        Loading.LoaderTask<int, DlLegacyFabricListResult> loader)
    {
        var result =
            (JsonObject)Requester.FetchJson("https://meta.legacyfabric.net/v2/versions");
        try
        {
            var output = new DlLegacyFabricListResult
            {
                isOfficial = true,
                sourceName = Lang.Text("Download.Source.LegacyFabricOfficial"),
                Value = result
            };
            if (output.Value["game"] is null
                || output.Value["loader"] is null
                || output.Value["installer"] is null)
                throw new Exception(
                    Lang.Text(
                        "Minecraft.Download.Error.VersionListOperationFailed",
                        "LegacyFabric",
                        result));
            loader.output = output;
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "LegacyFabric",
                    result),
                ex);
        }
    }

    public struct DlLegacyFabricListResult
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
