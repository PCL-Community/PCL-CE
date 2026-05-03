using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Network;

namespace PCL;

public class DlCleanroomList
{
    /// DlCleanroomList | Cleanroom 版本列表

    public struct DlCleanroomListResult
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
        ///     所有版本的列表。已经按从新到老排序。
        /// </summary>
        public List<DlCleanroomListEntry> Value;
    }

    public class DlCleanroomListEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     API 使用的原始版本字符串，如 “0.2.4-alpha”。
        /// </summary>
        public string ApiName;

        /// <summary>
        ///     是否是 Beta 版。
        /// </summary>
        public bool IsBeta;

        public DlCleanroomListEntry(string ApiName)
        {
            ForgeType = ForgelikeType.Cleanroom;
            this.ApiName = ApiName;
            IsBeta = ApiName.Contains("alpha");
            VersionName = ApiName;
            Version = new Version(ApiName.BeforeFirst("-"));
            Inherit = "1.12.2";
        }

        /// <summary>
        ///     文件在官网的基础地址，不包含后缀。
        /// </summary>
        public string UrlBase =>
            $"https://github.com/CleanroomMC/Cleanroom/releases/download/{ApiName}/cleanroom-{ApiName}";
    }

    /// <summary>
    ///     Cleanroom 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlCleanroomListResult> DlCleanroomListLoader =
        new("DlCleanroomList Main", DlCleanroomListMain);

    private static void DlCleanroomListMain(ModLoader.LoaderTask<int, DlCleanroomListResult> Loader)
    {
        var timeout = Config.Download.VersionListSource switch
        {
            0 => 30,
            1 => 5,
            _ => 60,
        };
        DlSource.DlSourceLoader(Loader,
            [new KeyValuePair<ModLoader.LoaderTask<int, DlCleanroomListResult>, int>(DlCleanroomListOfficialLoader, timeout)],
            Loader.IsForceRestarting);
    }

    /// <summary>
    ///     Cleanroom 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlCleanroomListResult> DlCleanroomListOfficialLoader =
        new("DlCleanroomList Official", DlCleanroomListOfficialMain);

    private static void DlCleanroomListOfficialMain(ModLoader.LoaderTask<int, DlCleanroomListResult> Loader)
    {
        // 获取版本列表 JSON
        var ResultLatest = Requester.FetchJson(
            "https://api.github.com/repos/CleanroomMC/Cleanroom/releases", new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        if (ResultLatest.Length < 100)
            throw new Exception("获取到的版本列表长度不足（" + ResultLatest + "）");
        // 解析
        try
        {
            Loader.Output = new DlCleanroomListResult
            {
                IsOfficial = true,
                SourceName = "Cleanroom 官方源",
                Value = GetCleanroomEntries(ResultLatest)
            };
        }
        catch (Exception ex)
        {
            throw new Exception("Cleanroom 官方源版本列表解析失败（" + ResultLatest + "）", ex);
        }
    }

    private static List<DlCleanroomListEntry> GetCleanroomEntries(string LatestJson)
    {
        var Versions = new List<DlCleanroomListEntry>();
        var Json = JArray.Parse(LatestJson);
        foreach (JObject Token in Json)
            Versions.Add(new DlCleanroomListEntry(Token["tag_name"].ToString()));
        if (!Versions.Any())
            throw new Exception("没有可用版本");
        Versions = Versions.OrderByDescending(a => a.Version).ToList();
        return Versions;
    }
}