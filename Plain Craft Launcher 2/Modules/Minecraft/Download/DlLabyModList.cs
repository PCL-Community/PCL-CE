using System.Net;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.IO.Net.Http;

namespace PCL;

public class DlLabyModList
{
    /// DlLabyModList | LabyMod 列表

    public struct DlLabyModListResult
    {
        /// <summary>
        ///     获取到的数据。
        /// </summary>
        public JObject Value;
    }

    /// <summary>
    ///     LabyMod 列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLabyModListResult> DlLabyModListLoader =
        new("DlLabyModList Main", DlLabyModListMain);

    private static void DlLabyModListMain(ModLoader.LoaderTask<int, DlLabyModListResult> Loader)
    {
        var firstTimeout = Config.Download.VersionListSource switch { 0 => 30, 1 => 5, _ => 60 };
        var secondTimeout = Config.Download.VersionListSource switch { 0 => 60, 1 => 35, _ => 60 };
        DlSource.DlSourceLoader(Loader,
            [
                new KeyValuePair<ModLoader.LoaderTask<int, DlLabyModListResult>, int>(DlLabyModListOfficialLoader, firstTimeout),
                new KeyValuePair<ModLoader.LoaderTask<int, DlLabyModListResult>, int>(DlLabyModListOfficialLoader, secondTimeout)
            ],
            Loader.IsForceRestarting);
    }

    /// <summary>
    ///     LabyMod 列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlLabyModListResult> DlLabyModListOfficialLoader =
        new("DlLabyModList Official", DlLabyModListOfficialMain);

    private static void DlLabyModListOfficialMain(ModLoader.LoaderTask<int, DlLabyModListResult> Loader)
    {
        JObject ResultProduction;
        using (var productionResponse = HttpRequest
                   .Create("https://releases.r2.labymod.net/api/v1/manifest/production/latest.json")
                   .WithHttpVersionOption(HttpVersion.Version20)
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            ResultProduction = (JObject)ModBase.GetJson(productionResponse.AsString());
        }

        JObject ResultSnapshot;
        using (var snapshotResponse = HttpRequest
                   .Create("https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json")
                   .WithHttpVersionOption(HttpVersion.Version20)
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            snapshotResponse.EnsureSuccessStatusCode();
            ResultSnapshot = (JObject)ModBase.GetJson(snapshotResponse.AsString());
        }

        var Result = new JObject();
        Result.Add("production", ResultProduction);
        Result.Add("snapshot", ResultSnapshot);
        try
        {
            var Output = new DlLabyModListResult { Value = Result };
            if (Output.Value["production"]["labyModVersion"] is null ||
                Output.Value["snapshot"]["labyModVersion"] is null)
                throw new Exception("获取到的列表缺乏必要项");
            Loader.Output = Output;
        }
        catch (Exception ex)
        {
            throw new Exception("LabyMod 版本列表解析失败（" + Result + "）", ex);
        }
    }
}