using System.Globalization;
using System.Net;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static partial class GameDownload
{
    /// <summary>
    ///     Forge 版本列表，主加载器。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlForgeListResult> DlForgeListLoader =
        new("DlForgeList Main", DlForgeListMain);

    /// <summary>
    ///     Forge 版本列表，官方源。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlForgeListResult> DlForgeListOfficialLoader =
        new("DlForgeList Official", DlForgeListOfficialMain);

    /// <summary>
    ///     Forge 版本列表，BMCLAPI。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlForgeListResult> DlForgeListBmclapiLoader =
        new("DlForgeList Bmclapi", DlForgeListBmclapiMain);

    private static void DlForgeListMain(Loading.LoaderTask<int, DlForgeListResult> loader)
    {
        DlSourceLoader(
            loader,
            _GetForgeListSources(),
            loader.isForceRestarting);
    }

    private static List<KeyValuePair<Loading.LoaderTask<int, DlForgeListResult>, int>>
        _GetForgeListSources()
    {
        return Config.Download.VersionListSource switch
        {
            0 =>
            [
                new KeyValuePair<Loading.LoaderTask<int, DlForgeListResult>, int>(
                    DlForgeListBmclapiLoader,
                    30),
                new KeyValuePair<Loading.LoaderTask<int, DlForgeListResult>, int>(
                    DlForgeListOfficialLoader,
                    30 + 60)
            ],
            1 =>
            [
                new KeyValuePair<Loading.LoaderTask<int, DlForgeListResult>, int>(
                    DlForgeListOfficialLoader,
                    5),
                new KeyValuePair<Loading.LoaderTask<int, DlForgeListResult>, int>(
                    DlForgeListBmclapiLoader,
                    5 + 30)
            ],
            _ =>
            [
                new KeyValuePair<Loading.LoaderTask<int, DlForgeListResult>, int>(
                    DlForgeListOfficialLoader,
                    60),
                new KeyValuePair<Loading.LoaderTask<int, DlForgeListResult>, int>(
                    DlForgeListBmclapiLoader,
                    60 + 60)
            ]
        };
    }

    private static void DlForgeListOfficialMain(Loading.LoaderTask<int, DlForgeListResult> loader)
    {
        var result = Requester.FetchString(
            "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html",
            new RequestParam
            {
                Encoding = Encoding.Default,
                UseBrowserUserAgent = true
            });
        if (result.Length < 200)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Forge",
                    result));

        var names = result.RegexSearch("(?<=a href=\"index_)[0-9.]+(_pre[0-9]?)?(?=.html)");
        names.Add("1.2.4");
        if (names.Count < 10)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Forge",
                    result));

        loader.output = new DlForgeListResult
        {
            isOfficial = true,
            sourceName = Lang.Text("Download.Source.ForgeOfficial"),
            Value = names
        };
    }

    private static void DlForgeListBmclapiMain(Loading.LoaderTask<int, DlForgeListResult> loader)
    {
        var result =
            Requester
                .FetchJson(
                    "https://bmclapi2.bangbang93.com/forge/minecraft",
                    new RequestParam
                    {
                        Encoding = Encoding.Default
                    })
                ?.ToString()
            ?? "";
        if (result.Length < 200)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Forge BMCLAPI",
                    result));

        var names = result.RegexSearch("[0-9.]+(_pre[0-9]?)?");
        if (names.Count < 10)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Forge BMCLAPI",
                    result));

        loader.output = new DlForgeListResult
        {
            isOfficial = false,
            sourceName = "BMCLAPI",
            Value = names
        };
    }

    /// <summary>
    ///     Forge 版本列表，主加载器。
    /// </summary>
    public static void DlForgeVersionMain(
        Loading.LoaderTask<string, List<DlForgeVersionEntry>> loader)
    {
        var officialLoader =
            new Loading.LoaderTask<string, List<DlForgeVersionEntry>>(
                "DlForgeVersion Official",
                DlForgeVersionOfficialMain);
        var bmclapiLoader =
            new Loading.LoaderTask<string, List<DlForgeVersionEntry>>(
                "DlForgeVersion Bmclapi",
                DlForgeVersionBmclapiMain);

        DlSourceLoader(
            loader,
            _GetForgeVersionSources(officialLoader, bmclapiLoader),
            loader.isForceRestarting);
    }

    private static List<KeyValuePair<Loading.LoaderTask<string, List<DlForgeVersionEntry>>, int>>
        _GetForgeVersionSources(
            Loading.LoaderTask<string, List<DlForgeVersionEntry>> officialLoader,
            Loading.LoaderTask<string, List<DlForgeVersionEntry>> bmclapiLoader)
    {
        return Config.Download.VersionListSource switch
        {
            0 =>
            [
                new KeyValuePair<Loading.LoaderTask<string, List<DlForgeVersionEntry>>, int>(
                    bmclapiLoader,
                    30),
                new KeyValuePair<Loading.LoaderTask<string, List<DlForgeVersionEntry>>, int>(
                    officialLoader,
                    30 + 60)
            ],
            1 =>
            [
                new KeyValuePair<Loading.LoaderTask<string, List<DlForgeVersionEntry>>, int>(
                    officialLoader,
                    5),
                new KeyValuePair<Loading.LoaderTask<string, List<DlForgeVersionEntry>>, int>(
                    bmclapiLoader,
                    5 + 30)
            ],
            _ =>
            [
                new KeyValuePair<Loading.LoaderTask<string, List<DlForgeVersionEntry>>, int>(
                    officialLoader,
                    60),
                new KeyValuePair<Loading.LoaderTask<string, List<DlForgeVersionEntry>>, int>(
                    bmclapiLoader,
                    60 + 60)
            ]
        };
    }

    /// <summary>
    ///     Forge 版本列表，官方源。
    /// </summary>
    public static void DlForgeVersionOfficialMain(
        Loading.LoaderTask<string, List<DlForgeVersionEntry>> loader)
    {
        string result;
        var inherit = loader.input ?? "";
        try
        {
            result = Requester.FetchString(
                $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_{inherit.Replace("-", "_")}.html",
                new RequestParam
                {
                    UseBrowserUserAgent = true
                });
        }
        catch (WebException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("(404)"))
                throw new NotFoundException();

            throw;
        }

        if (result.Length < 1000)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Forge",
                    result));

        List<DlForgeVersionEntry> versions;
        try
        {
            versions = _ParseOfficialForgeVersions(result, inherit);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text("Minecraft.Download.Error.VersionListOperationFailed", "Forge", result),
                ex);
        }

        if (versions.Count == 0)
            throw new NotFoundException();
        loader.output = versions;
    }

    private static List<DlForgeVersionEntry> _ParseOfficialForgeVersions(
        string result,
        string inherit)
    {
        var versions = new List<DlForgeVersionEntry>();
        var versionCodes = result[..result.LastIndexOfF("</table>")]
            .Split("<td class=\"download-version");
        for (int i = 1, loopTo = versionCodes.Length - 1; i <= loopTo; i++)
        {
            var versionCode = versionCodes[i];
            try
            {
                var entry = _ParseOfficialForgeVersion(versionCode, inherit);
                if (entry is not null)
                    versions.Add(entry);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    Lang.Text(
                        "Minecraft.Download.Error.VersionListOperationFailed",
                        "Forge",
                        versionCode),
                    ex);
            }
        }

        return versions;
    }

    private static DlForgeVersionEntry? _ParseOfficialForgeVersion(
        string versionCode,
        string inherit)
    {
        var name = versionCode.RegexSeek(@"(?<=[^(0-9)]+)[0-9\.]+");
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var isRecommended = versionCode.Contains("fa promo-recommended");

        var branch =
            versionCode.RegexSeek($"(?<=-{name}-)[^-\"]+(?=-[a-z]+.[a-z]{{3}})");
        if (string.IsNullOrWhiteSpace(branch))
            branch = null;

        var releaseTimeOriginal =
            versionCode.RegexSeek("(?<=\"download-time\" title=\")[^\"]+");
        if (string.IsNullOrWhiteSpace(releaseTimeOriginal))
            return null;
        var releaseDate = DateTime.Parse(
            releaseTimeOriginal,
            null,
            DateTimeStyles.AssumeUniversal);
        var releaseTime = releaseDate.ToLocalTime();

        var artifact = _ParseOfficialForgeArtifact(versionCode);
        if (artifact is null)
            return null;

        return new DlForgeVersionEntry(name, branch, inherit)
        {
            Category = artifact.Value.Category,
            IsRecommended = isRecommended,
            Hash = artifact.Value.Hash.Trim('\r', '\n'),
            ReleaseTime = releaseTime
        };
    }

    private static (string Hash, string Category)? _ParseOfficialForgeArtifact(
        string versionCode)
    {
        string marker;
        string category;
        if (versionCode.Contains("classifier-installer\""))
        {
            marker = "installer.jar";
            category = "installer";
        }
        else if (versionCode.Contains("classifier-universal\""))
        {
            marker = "universal.zip";
            category = "universal";
        }
        else if (versionCode.Contains("client.zip"))
        {
            marker = "client.zip";
            category = "client";
        }
        else
        {
            return null;
        }

        var artifactCode = versionCode[versionCode.IndexOfF(marker)..];
        return (artifactCode.RegexSeek("(?<=MD5:</strong> )[^<]+") ?? "", category);
    }

    /// <summary>
    ///     Forge 版本列表，BMCLAPI。
    /// </summary>
    public static void DlForgeVersionBmclapiMain(
        Loading.LoaderTask<string, List<DlForgeVersionEntry>> loader)
    {
        var inherit = loader.input ?? "";
        var json = (JsonArray)Requester.FetchJson(
            $"https://bmclapi2.bangbang93.com/forge/minecraft/{inherit.Replace("-", "_")}");

        List<DlForgeVersionEntry> versions;
        try
        {
            versions = _ParseBmclapiForgeVersions(json, inherit);
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Forge BMCLAPI",
                    json),
                ex);
        }

        if (versions.Count == 0)
            throw new NotFoundException();
        loader.output = versions;
    }

    private static List<DlForgeVersionEntry> _ParseBmclapiForgeVersions(
        JsonArray json,
        string inherit)
    {
        var recommended = DownloadSources.ForgeRecommendedGet(inherit);
        return
        [
            ..
            from token in json.OfType<JsonObject>()
            let artifact = _FindBmclapiForgeArtifact(token)
            let branch = (string?)token["branch"]
            let name = token["version"]?.ToString() ?? ""
            select new DlForgeVersionEntry(name, branch, inherit)
            {
                Hash = artifact.Hash,
                Category = artifact.Category,
                IsRecommended = (recommended ?? "") == name,
                ReleaseTime = token["modified"].ToObject<DateTime>().ToLocalTime()
            }
        ];
    }

    private static (string Hash, string Category) _FindBmclapiForgeArtifact(JsonObject token)
    {
        string? hash = null;
        var category = "unknown";
        var priority = -1;
        foreach (var file in (token["files"]?.AsArray() ?? []).OfType<JsonObject>())
            switch (file["category"]?.ToString() ?? "")
            {
                case "installer":
                {
                    if (file["format"]?.ToString() == "jar")
                    {
                        hash = (string?)file["hash"];
                        category = "installer";
                        priority = 2;
                    }

                    break;
                }
                case "universal":
                {
                    if (priority <= 1 && file["format"]?.ToString() == "zip")
                    {
                        hash = (string?)file["hash"];
                        category = "universal";
                        priority = 1;
                    }

                    break;
                }
                case "client":
                {
                    if (priority <= 0 && file["format"]?.ToString() == "zip")
                    {
                        hash = (string?)file["hash"];
                        category = "client";
                        priority = 0;
                    }

                    break;
                }
            }

        return (hash ?? "", category);
    }

    public struct DlForgeListResult
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
        public List<string> Value;
    }

    public abstract class DlForgelikeEntry(
        string? inherit = null,
        string? versionName = null,
        DlForgelikeEntry.ForgelikeType forgeType = default,
        Version? version = null) : IComparable<DlForgelikeEntry>
    {
        public enum ForgelikeType
        {
            Forge,
            NeoForge,
            Cleanroom
        }

        /// <summary>
        ///     对应的 Minecraft 版本，如“1.12.2”。
        /// </summary>
        public string Inherit = inherit ?? "";

        /// <summary>
        ///     可对玩家显示的非格式化版本名。
        ///     Forge：如 “50.1.9”、“14.22.1.2478”（Legacy）。
        ///     NeoForge：如 “20.4.30-beta”、“47.1.99”（Legacy）。
        ///     Cleanroom：如 “0.2.4-alpha”。
        /// </summary>
        public string VersionName = versionName ?? "";

        /// <summary>
        ///     Forgelike 种类。Forge、NeoForge、Cleanroom。
        /// </summary>
        public ForgelikeType forgeType = forgeType;

        /// <summary>
        ///     标准化后的版本号，仅可用于比较与排序。
        ///     格式：Major.Minor.Build.Revision
        ///     Forge：如 “50.1.9.0”（最后一位固定为 0）、“14.22.1.2478”（Legacy）。
        ///     NeoForge：如 “20.4.30.0”（最后一位固定为 0）、“19.47.1.99”（Legacy：第一位固定为 19）。
        ///     Cleanroom：如 “0.2.4.1”（Alpha：最后一位固定为 1）。
        /// </summary>
        public Version version = version ?? new Version();

        /// <summary>
        ///     加载器名称。Forge / NeoForge / Cleanroom。
        /// </summary>
        public string LoaderName => forgeType.ToString();

        /// <summary>
        ///     文件扩展名。不以小数点开头。
        /// </summary>
        public string FileExtension =>
            forgeType switch
            {
                ForgelikeType.Forge when ((DlForgeVersionEntry)this).Category == "installer" =>
                    "jar",
                ForgelikeType.Forge => "zip",
                _ => "jar"
            };

        /// <summary>
        ///     Forge：MC 版本是否小于 1.13。
        ///     NeoForge：MC 版本是否为 1.20.1。
        ///     Cleanroom：固定为 False。
        /// </summary>
        public bool IsLegacy => (int)forgeType != 2 && version.Major < 20;

        public int CompareTo(DlForgelikeEntry? other)
        {
            if (other is null) return 1;
            return version != other.version
                ? version.CompareTo(other.version)
                : McVersionComparer.CompareVersion(VersionName, other.VersionName);
        }
    }

    public class DlForgeVersionEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     用于下载的文件版本名。可能在 Version 的基础上添加了分支。
        /// </summary>
        public readonly string FileVersion;

        /// <summary>
        ///     安装类型。有 installer、client、universal 三种。
        /// </summary>
        public string Category = "";

        /// <summary>
        ///     文件的 MD5 或 SHA1（BMCLAPI 的老版本是 MD5，新版本是 SHA1；官方源总是 MD5）。
        /// </summary>
        public string Hash = "";

        /// <summary>
        ///     是否为推荐版本。
        /// </summary>
        public bool IsRecommended;

        /// <summary>
        ///     发布时间。<br />
        ///     该字段保存原始时间，展示时请使用 <see cref="Lang.Date" /> 按当前区域性格式化。
        /// </summary>
        public DateTime? ReleaseTime;

        public DlForgeVersionEntry(
            string version,
            string? branch,
            string inherit)
        {
            if (version is "11.15.1.2318" or "11.15.1.1902" or "11.15.1.1890")
                branch = "1.8.9";
            if (branch is null
                && inherit == "1.7.10"
                && double.Parse(version.Split(".")[3]) >= 1300d)
                branch = "1.7.10";
            forgeType = ForgelikeType.Forge;
            VersionName = version;
            this.version = new Version(version);
            Inherit = inherit;
            FileVersion = version + (branch is null ? "" : $"-{branch}");
        }
    }
}