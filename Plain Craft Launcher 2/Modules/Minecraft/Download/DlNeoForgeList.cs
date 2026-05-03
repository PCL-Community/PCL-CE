using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public class DlNeoForgeList
{
    /// DlNeoForgeList | NeoForge 版本列表

    public struct DlNeoForgeListResult
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
        public List<DlNeoForgeListEntry> Value;
    }

    public class DlNeoForgeListEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     API 使用的原始版本字符串，如 “20.4.30-beta”、“1.20.1-47.1.99”（Legacy）。
        /// </summary>
        public string ApiName;

        /// <summary>
        ///     是否是 Beta 版。
        /// </summary>
        public bool IsBeta;

        public DlNeoForgeListEntry(string ApiName)
        {
            ForgeType = ForgelikeType.NeoForge;
            this.ApiName = ApiName;
            IsBeta = ApiName.Contains("beta") || ApiName.Contains("alpha");
            if (ApiName.Contains("1.20.1")) // 1.20.1-47.1.99
            {
                VersionName = ApiName.Replace("1.20.1-", "");
                Version = new Version("19." + VersionName);
                Inherit = "1.20.1";
            }
            else if (ApiName.StartsWith("0.")) // 0.25w14craftmine.3-beta
            {
                VersionName = ApiName;
                var Segments = ApiName.BeforeFirst("-").Split('.');
                Version = new Version(0, 0, int.Parse(Segments.Last()));
                Inherit = Segments[1];
            }
            else // 20.4.30-beta；26.1.0.0-alpha.1+snapshot-1
            {
                VersionName = ApiName;
                Version = new Version(ApiName.BeforeFirst("-"));
                if (Version.Major >= 24)
                    Inherit = $"{Version.Major}.{Version.Minor}{(Version.Build > 0 ? $".{Version.Build}" : "")}";
                else
                    Inherit = "1." + Version.Major + (Version.Minor > 0 ? "." + Version.Minor : "");
                if (VersionName.Contains("+"))
                    Inherit += "-" + VersionName.AfterFirst("+");
            }
        }

        /// <summary>
        ///     文件在官网的基础地址，不包含后缀。
        /// </summary>
        public string UrlBase
        {
            get
            {
                var PackageName = IsLegacy ? "forge" : "neoforge";
                return
                    $"https://maven.neoforged.net/releases/net/neoforged/{PackageName}/{ApiName}/{PackageName}-{ApiName}";
            }
        }
    }

    /// <summary>
    ///     NeoForge 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListLoader =
        new("DlNeoForgeList Main", DlNeoForgeListMain);

    private static void DlNeoForgeListMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        DlSource.DlSourceLoader(loader,
            DlSource.DlSourceVersionListGet(DlNeoForgeListOfficialLoader, DlNeoForgeListBmclapiLoader),
            loader.IsForceRestarting);
    }

    /// <summary>
    ///     NeoForge 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListOfficialLoader =
        new("DlNeoForgeList Official", DlNeoForgeListOfficialMain);

    private static void DlNeoForgeListOfficialMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        // 获取版本列表 JSON
        var resultLatest = Requester.FetchJson(
            "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        var resultLegacy = Requester.FetchJson(
            "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        if (resultLatest.Length < 100 || resultLegacy.Length < 100)
            throw new Exception("获取到的版本列表长度不足（" + resultLatest + "）");
        // 解析
        try
        {
            loader.Output = new DlNeoForgeListResult
            {
                IsOfficial = true,
                SourceName = "NeoForge 官方源",
                Value = GetNeoForgeEntries(resultLatest, resultLegacy)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                "NeoForge 官方源版本列表解析失败（" + resultLatest + "\r\n" + "\r\n" + resultLegacy + "）", ex);
        }
    }

    /// <summary>
    ///     NeoForge 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListBmclapiLoader =
        new("DlNeoForgeList Bmclapi", DlNeoForgeListBmclapiMain);

    public static void DlNeoForgeListBmclapiMain(ModLoader.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        // 获取版本列表 JSON
        var resultLatest = Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        var resultLegacy = Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge",
            new RequestParam
            {
                UseBrowserUserAgent = true
            }).ToString();
        if (resultLatest.Length < 100 || resultLegacy.Length < 100)
            throw new Exception("获取到的版本列表长度不足（" + resultLatest + "）");
        // 解析
        try
        {
            loader.Output = new DlNeoForgeListResult
            {
                IsOfficial = true,
                SourceName = "BMCLAPI",
                Value = GetNeoForgeEntries(resultLatest, resultLegacy)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                "NeoForge BMCLAPI 版本列表解析失败（" + resultLatest + "\r\n" + "\r\n" + resultLegacy + "）",
                ex);
        }
    }

    private static List<DlNeoForgeListEntry> GetNeoForgeEntries(string latestJson, string latestLegacyJson)
    {
        var versionNames = ModBase.RegexSearch(latestLegacyJson + latestJson, RegexPatterns.DlNeoForgeVersion);
        var versions = versionNames.Where(name => name != "47.1.82").Select(name => new DlNeoForgeListEntry(name))
            .OrderByDescending(a => a).ToList(); // 这个版本虽然在版本列表中，但不能下载
        if (!versions.Any())
            throw new Exception("无可用版本");
        return versions;
    }
}