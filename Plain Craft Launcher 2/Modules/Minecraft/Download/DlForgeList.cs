using System.Text;
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

    /// <summary>
    ///     Forge 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListOfficialLoader =
        new("DlForgeList Official", DlForgeListOfficialMain);

    private static void DlForgeListOfficialMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
    {
        var Result = Requester.FetchJson(
            "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html", new RequestParam
            {
                Encoding = Encoding.Default,
                UseBrowserUserAgent = true
            })?.ToString() ?? "";
        if (Result.Length < 200)
            throw new Exception("获取到的版本列表长度不足（" + Result + "）");
        // 获取所有版本信息
        var Names = Result.RegexSearch("(?<=a href=\"index_)[0-9.]+(_pre[0-9]?)?(?=.html)");
        Names.Add("1.2.4"); // 1.2.4 不会被匹配上
        if (Names.Count < 10)
            throw new Exception("获取到的版本数量不足（" + Result + "）");
        Loader.Output = new DlForgeListResult { IsOfficial = true, SourceName = "Forge 官方源", Value = Names };
    }

    /// <summary>
    ///     Forge 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlForgeListResult> DlForgeListBmclapiLoader =
        new("DlForgeList Bmclapi", DlForgeListBmclapiMain);

    private static void DlForgeListBmclapiMain(ModLoader.LoaderTask<int, DlForgeListResult> Loader)
    {
        var Result =
                Requester.FetchJson("https://bmclapi2.bangbang93.com/forge/minecraft",
                new RequestParam
                {
                    Encoding = Encoding.Default,
                })?.ToString() ?? "";
        if (Result.Length < 200)
            throw new Exception("获取到的版本列表长度不足（" + Result + "）");
        // 获取所有版本信息
        var Names = Result.RegexSearch("[0-9.]+(_pre[0-9]?)?");
        if (Names.Count < 10)
            throw new Exception("获取到的版本数量不足（" + Result + "）");
        Loader.Output = new DlForgeListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Names };
    }
}