using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.Download;
using PCL.Network;

namespace PCL;

public class DlOptiFineList
{
    /// DlOptiFineList | OptiFine 版本列表

    public struct DlOptiFineListResult
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
        public List<DlOptiFineListEntry> Value;
    }

    public class DlOptiFineListEntry
    {
        private string _inherit;

        /// <summary>
        ///     显示名称，已去除 HD_U 字样，如“1.12.2 C8”。
        /// </summary>
        public string DisplayName;

        /// <summary>
        ///     是否为测试版。
        /// </summary>
        public bool IsPreview;

        /// <summary>
        ///     原始文件名称，如“preview_OptiFine_1.11_HD_U_E1_pre.jar”。
        /// </summary>
        public string NameFile;

        /// <summary>
        ///     对应的版本名称，如“1.13.2-OptiFine_HD_U_E6”。
        /// </summary>
        public string NameVersion;

        /// <summary>
        ///     发布时间，格式为“yyyy/mm/dd”。OptiFine 源无此数据。
        /// </summary>
        public string ReleaseTime;

        /// <summary>
        ///     需要的最低 Forge 版本。空字符串为无限制，Nothing 为不兼容，“28.1.56” 表示版本号，“1161” 表示版本号的最后一位。
        /// </summary>
        public string RequiredForgeVersion;

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string Inherit
        {
            get => _inherit;
            set
            {
                if (value.EndsWithF(".0"))
                    value = Strings.Left(value, value.Length - 2);
                _inherit = value;
            }
        }
    }

    /// <summary>
    ///     OptiFine 版本列表，主加载器。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListLoader =
        new("DlOptiFineList Main", DlOptiFineListMain);

    private static void DlOptiFineListMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
    {
        DlSource.DlSourceLoader(Loader,
            DlSource.DlSourceVersionListGet(DlOptiFineListOfficialLoader, DlOptiFineListBmclapiLoader),
            Loader.IsForceRestarting);
    }

    /// <summary>
    ///     OptiFine 版本列表，官方源。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListOfficialLoader =
        new("DlOptiFineList Official", DlOptiFineListOfficialMain);

    private static void DlOptiFineListOfficialMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
    {
        string Result = "";
        using var resp = HttpRequest
            .Create(DownloadRegistry.OptiFineList)
            .WithHeader("Accept", "application/json, text/javascript, */*; q=0.01")
            .WithHeader("Accept-Language", "en-US,en;q=0.5")
            .WithHeader("X-Requested-With", "XMLHttpRequest")
            .SendAsync()
            .GetAwaiter()
            .GetResult();
        resp.EnsureSuccessStatusCode();
        Result = resp.AsString();
        if (Result.Length < 200)
            throw new Exception("获取到的版本列表长度不足（" + Result + "）");
        try
        {
            // 获取所有版本信息
            var Forge = Result.RegexSearch("(?<=colForge'>)[^<]*");
            var ReleaseTime = Result.RegexSearch("(?<=colDate'>)[^<]+");
            var Name = Result.RegexSearch("(?<=OptiFine_)[0-9A-Za-z_.]+(?=.jar\")");
            if (!(ReleaseTime.Count == Name.Count))
                throw new Exception("版本与发布时间数据无法对应");
            if (!(Forge.Count == Name.Count))
                throw new Exception("版本与 Forge 兼容数据无法对应");
            if (ReleaseTime.Count < 10)
                throw new Exception("获取到的版本数量不足（" + Result + "）");
            // 转化为列表输出
            var Versions = new List<DlOptiFineListEntry>();
            for (int i = 0, loopTo = ReleaseTime.Count - 1; i <= loopTo; i++)
            {
                Name[i] = Name[i].Replace("_", " ");
                var Entry = new DlOptiFineListEntry
                {
                    DisplayName = Name[i].Replace("HD U ", "").Replace(".0 ", " "),
                    ReleaseTime = new[]
                            { ReleaseTime[i].Split(".")[2], ReleaseTime[i].Split(".")[1], ReleaseTime[i].Split(".")[0] }
                        .Join("/"),
                    IsPreview = Name[i].ContainsF("pre", true),
                    Inherit = Name[i].Split(" ")[0],
                    NameFile = (Name[i].ContainsF("pre", true) ? "preview_" : "") + "OptiFine_" +
                               Name[i].Replace(" ", "_") + ".jar",
                    RequiredForgeVersion = Forge[i].Replace("Forge ", "").Replace("#", "")
                };
                if (Entry.RequiredForgeVersion.Contains("N/A"))
                    Entry.RequiredForgeVersion = null;
                Entry.NameVersion = Entry.Inherit + "-OptiFine_" +
                                    Name[i].Replace(" ", "_").Replace(Entry.Inherit + "_", "");
                Versions.Add(Entry);
            }

            Loader.Output = new DlOptiFineListResult
                { IsOfficial = true, SourceName = "OptiFine 官方源", Value = Versions };
        }
        catch (Exception ex)
        {
            throw new Exception("OptiFine 官方源版本列表解析失败（" + Result + "）", ex);
        }
    }

    /// <summary>
    ///     OptiFine 版本列表，BMCLAPI。
    /// </summary>
    public static ModLoader.LoaderTask<int, DlOptiFineListResult> DlOptiFineListBmclapiLoader =
        new("DlOptiFineList Bmclapi", DlOptiFineListBmclapiMain);

    private static void DlOptiFineListBmclapiMain(ModLoader.LoaderTask<int, DlOptiFineListResult> Loader)
    {
        var Json = (JArray)Requester.FetchJson(DownloadProvider.VersionList.ToBmclapiUrl(DownloadRegistry.OptiFineList));
        try
        {
            var Versions = new List<DlOptiFineListEntry>();
            foreach (JObject Token in Json)
            {
                var Entry = new DlOptiFineListEntry
                {
                    DisplayName =
                        (Token["mcversion"] + Token["type"].ToString().Replace("HD_U", "").Replace("_", " ") + " " +
                         Token["patch"]).Replace(".0 ", " "),
                    ReleaseTime = "",
                    IsPreview = Token["patch"].ToString().ContainsF("pre", true),
                    Inherit = Token["mcversion"].ToString(),
                    NameFile = Token["filename"].ToString(),
                    RequiredForgeVersion = (Token["forge"] ?? "").ToString().Replace("Forge ", "").Replace("#", "")
                };
                if (Entry.RequiredForgeVersion.Contains("N/A"))
                    Entry.RequiredForgeVersion = null;
                Entry.NameVersion = Entry.Inherit + "-OptiFine_" + (Token["type"] + " " + Token["patch"])
                    .Replace(".0 ", " ").Replace(" ", "_").Replace(Entry.Inherit + "_", "");
                Versions.Add(Entry);
            }

            Loader.Output = new DlOptiFineListResult { IsOfficial = false, SourceName = "BMCLAPI", Value = Versions };
        }
        catch (Exception ex)
        {
            throw new Exception("OptiFine BMCLAPI 版本列表解析失败（" + Json + "）", ex);
        }
    }
}