using System.Globalization;
using System.Net;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using PCL.Network;

namespace PCL;

public class DlForgeVersion
{
        #region DlForgeVersion | Forge 版本列表

    public abstract class DlForgelikeEntry : IComparable<DlForgelikeEntry>
    {
        public enum ForgelikeType
        {
            Forge,
            NeoForge,
            Cleanroom
        }

        /// <summary>
        ///     Forgelike 种类。Forge、NeoForge、Cleanroom。
        /// </summary>
        public ForgelikeType ForgeType;

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string Inherit;

        /// <summary>
        ///     标准化后的版本号，仅可用于比较与排序。
        ///     格式：Major.Minor.Build.Revision
        ///     Forge：如 “50.1.9.0”（最后一位固定为 0）、“14.22.1.2478”（Legacy）。
        ///     NeoForge：如 “20.4.30.0”（最后一位固定为 0）、“19.47.1.99”（Legacy：第一位固定为 19）。
        ///     Cleanroom：如 “0.2.4.1”（Alpha：最后一位固定为 1）。
        /// </summary>
        public Version Version;

        /// <summary>
        ///     可对玩家显示的非格式化版本名。
        ///     Forge：如 “50.1.9”、“14.22.1.2478”（Legacy）。
        ///     NeoForge：如 “20.4.30-beta”、“47.1.99”（Legacy）。
        ///     Cleanroom：如 “0.2.4-alpha”。
        /// </summary>
        public string VersionName;

        /// <summary>
        ///     加载器名称。Forge / NeoForge / Cleanroom。
        /// </summary>
        public string LoaderName => ForgeType.ToString();

        /// <summary>
        ///     文件扩展名。不以小数点开头。
        /// </summary>
        public string FileExtension
        {
            get
            {
                if (ForgeType == 0) return ((DlForgeVersionEntry)this).Category == "installer" ? "jar" : "zip";

                return "jar";
            }
        }

        /// <summary>
        ///     Forge：MC 版本是否小于 1.13。
        ///     NeoForge：MC 版本是否为 1.20.1。
        ///     Cleanroom：固定为 False。
        /// </summary>
        public bool IsLegacy
        {
            get
            {
                // Cleanroom 始终为 False
                if ((int)ForgeType == 2)
                    return false;
                // 虽然很抽象，但确实可以这样判断
                // Forge：1.13+ 的版本号首位都大于 20
                // NeoForge：1.20.1 的版本号首位人为规定为 19 开头
                return Version.Major < 20;
            }
        }

        public int CompareTo(DlForgelikeEntry other)
        {
            if (Version != other.Version) return Version.CompareTo(other.Version);

            return ModMinecraft.CompareVersion(VersionName, other.VersionName);
        }
    }

    public class DlForgeVersionEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     安装类型。有 installer、client、universal 三种。
        /// </summary>
        public string Category;

        /// <summary>
        ///     用于下载的文件版本名。可能在 Version 的基础上添加了分支。
        /// </summary>
        public string FileVersion;

        /// <summary>
        ///     文件的 MD5 或 SHA1（BMCLAPI 的老版本是 MD5，新版本是 SHA1；官方源总是 MD5）。
        /// </summary>
        public string Hash;

        /// <summary>
        ///     是否为推荐版本。
        /// </summary>
        public bool IsRecommended;

        /// <summary>
        ///     发布时间，格式为“yyyy/MM/dd HH:mm”。
        /// </summary>
        public string ReleaseTime;

        public DlForgeVersionEntry(string Version, string Branch, string Inherit)
        {
            // 司马版本的特殊处理
            if (Version == "11.15.1.2318" || Version == "11.15.1.1902" || Version == "11.15.1.1890")
                Branch = "1.8.9";
            if (Branch is null && Inherit == "1.7.10" && double.Parse(Version.Split(".")[3]) >= 1300)
                Branch = "1.7.10";
            // 为 DlForgelikeEntry 提供所有信息
            ForgeType = ForgelikeType.Forge;
            VersionName = Version;
            this.Version = new Version(Version);
            this.Inherit = Inherit;
            FileVersion = Version + (Branch is null ? "" : "-" + Branch);
        }
    }

    /// <summary>
    ///     Forge 版本列表，主加载器。
    /// </summary>
    public static void DlForgeVersionMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
    {
        var DlForgeVersionOfficialLoader =
            new ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>("DlForgeVersion Official",
                DlForgeVersionOfficialMain);
        var DlForgeVersionBmclapiLoader =
            new ModLoader.LoaderTask<string, List<DlForgeVersionEntry>>("DlForgeVersion Bmclapi",
                DlForgeVersionBmclapiMain);
        DlSource.DlSourceLoader(Loader,
            DlSource.DlSourceVersionListGet(DlForgeVersionOfficialLoader, DlForgeVersionBmclapiLoader),
            Loader.IsForceRestarting);
    }

    /// <summary>
    ///     Forge 版本列表，官方源。
    /// </summary>
    public static void DlForgeVersionOfficialMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
    {
        string Result;
        try
        {
            Result = Requester.FetchJson(
                "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_" +
                Loader.Input.Replace("-", "_") + ".html", new RequestParam
                {
                    UseBrowserUserAgent = true
                })?.ToString() ?? ""; // 兼容 Forge 1.7.10-pre4，#4057
        }
        catch (WebException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("(404)")) throw new Exception("无可用版本");

            throw;
        }

        if (Result.Length < 1000)
            throw new Exception("获取到的版本列表长度不足（" + Result + "）");
        var Versions = new List<DlForgeVersionEntry>();
        try
        {
            // 分割版本信息
            var VersionCodes = Strings.Mid(Result, 1, Result.LastIndexOfF("</table>"))
                .Split("<td class=\"download-version");
            // 获取所有版本信息
            for (int i = 1, loopTo = VersionCodes.Count() - 1; i <= loopTo; i++)
            {
                var VersionCode = VersionCodes[i];
                try
                {
                    // 基础信息获取
                    var Name = VersionCode.RegexSeek(@"(?<=[^(0-9)]+)[0-9\.]+");
                    var IsRecommended = VersionCode.Contains("fa promo-recommended");
                    var Inherit = Loader.Input;
                    // 分支获取
                    var Branch = VersionCode.RegexSeek($"(?<=-{Name}-)[^-\"]+(?=-[a-z]+.[a-z]{{3}})");
                    if (string.IsNullOrWhiteSpace(Branch))
                        Branch = null;
                    // 发布时间获取
                    var ReleaseTimeOriginal = VersionCode.RegexSeek("(?<=\"download-time\" title=\")[^\"]+");
                    // Dim ReleaseTimeSplit = ReleaseTimeOriginal.Split(" -:".ToCharArray) '原格式："2021-02-15 03:24:02"
                    var ReleaseDate =
                        DateTime.Parse(ReleaseTimeOriginal, null, DateTimeStyles.AssumeUniversal); // 以 UTC 时间作为标准
                    var ReleaseTime = ReleaseDate.ToLocalTime().ToString("yyyy'/'MM'/'dd HH':'mm"); // 时区与格式转换
                    // 分类与 MD5 获取
                    string MD5;
                    string Category;
                    if (VersionCode.Contains("classifier-installer\""))
                    {
                        // 类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("installer.jar"));
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                        Category = "installer";
                    }
                    else if (VersionCode.Contains("classifier-universal\""))
                    {
                        // 类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("universal.zip"));
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                        Category = "universal";
                    }
                    else if (VersionCode.Contains("client.zip"))
                    {
                        // 类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                        VersionCode = VersionCode.Substring(VersionCode.IndexOfF("client.zip"));
                        MD5 = VersionCode.RegexSeek("(?<=MD5:</strong> )[^<]+");
                        Category = "client";
                    }
                    else
                    {
                        // 没有任何下载（1.6.4 有一部分这种情况）
                        continue;
                    }

                    // 添加进列表
                    Versions.Add(new DlForgeVersionEntry(Name, Branch, Inherit)
                    {
                        Category = Category, IsRecommended = IsRecommended,
                        Hash = MD5.Trim('\r', '\n'),
                        ReleaseTime = ReleaseTime
                    });
                }
                catch (Exception ex)
                {
                    throw new Exception("Forge 官方源版本信息提取失败（" + VersionCode + "）", ex);
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Forge 官方源版本列表解析失败（" + Result + "）", ex);
        }

        if (!Versions.Any())
            throw new Exception("无可用版本");
        Loader.Output = Versions;
    }

    /// <summary>
    ///     Forge 版本列表，BMCLAPI。
    /// </summary>
    public static void DlForgeVersionBmclapiMain(ModLoader.LoaderTask<string, List<DlForgeVersionEntry>> Loader)
    {
        var Json = (JArray)Requester.FetchJson(
            "https://bmclapi2.bangbang93.com/forge/minecraft/" +
            Loader.Input.Replace("-", "_")); // 兼容 Forge 1.7.10-pre4，#4057
        var Versions = new List<DlForgeVersionEntry>();
        try
        {
            var Recommended = ModDownloadLib.McDownloadForgeRecommendedGet(Loader.Input);
            foreach (JObject Token in Json)
            {
                // 分类与 Hash 获取
                string Hash = null;
                var Category = "unknown";
                var Proi = -1;
                foreach (JObject File in Token["files"])
                    switch (File["category"].ToString() ?? "")
                    {
                        case "installer":
                        {
                            if (File["format"].ToString() == "jar")
                            {
                                // 类型为 installer.jar，支持范围 ~753 (~ 1.6.1 部分), 738~684 (1.5.2 全部)
                                Hash = (string)File["hash"];
                                Category = "installer";
                                Proi = 2;
                            }

                            break;
                        }
                        case "universal":
                        {
                            if (Proi <= 1 && File["format"].ToString() == "zip")
                            {
                                // 类型为 universal.zip，支持范围 751~449 (1.6.1 部分), 682~183 (1.5.1 ~ 1.3.2 部分)
                                Hash = (string)File["hash"];
                                Category = "universal";
                                Proi = 1;
                            }

                            break;
                        }
                        case "client":
                        {
                            if (Proi <= 0 && File["format"].ToString() == "zip")
                            {
                                // 类型为 client.zip，支持范围 182~ (1.3.2 部分 ~)
                                Hash = (string)File["hash"];
                                Category = "client";
                                Proi = 0;
                            }

                            break;
                        }
                    }

                // 获取 Entry
                var Branch = (string)Token["branch"];
                var Name = (string)Token["version"];
                // 基础信息获取
                var Entry = new DlForgeVersionEntry(Name, Branch, Loader.Input)
                    { Hash = Hash, Category = Category, IsRecommended = (Recommended ?? "") == (Name ?? "") };
                var TimeSplit = Token["modified"].ToString().Split('-', 'T', ':', '.', ' ', '/');
                Entry.ReleaseTime = Token["modified"].ToObject<DateTime>().ToLocalTime()
                    .ToString("yyyy'/'MM'/'dd HH':'mm");
                // 添加项
                Versions.Add(Entry);
            }
        }
        catch (Exception ex)
        {
            throw new Exception("Forge BMCLAPI 版本列表解析失败（" + Json + "）", ex);
        }

        if (!Versions.Any())
            throw new Exception("无可用版本");
        Loader.Output = Versions;
    }

    #endregion
}