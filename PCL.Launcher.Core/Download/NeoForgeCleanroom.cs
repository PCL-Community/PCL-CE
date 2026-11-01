using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static partial class GameDownload
{
    /// <summary>
    ///     NeoForge 版本列表，主加载器。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlNeoForgeListResult> DlNeoForgeListLoader =
        new("DlNeoForgeList Main", DlNeoForgeListMain);

    /// <summary>
    ///     NeoForge 版本列表，官方源。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlNeoForgeListResult>
        DlNeoForgeListOfficialLoader =
            new("DlNeoForgeList Official", DlNeoForgeListOfficialMain);

    /// <summary>
    ///     NeoForge 版本列表，BMCLAPI。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlNeoForgeListResult>
        DlNeoForgeListBmclapiLoader =
            new("DlNeoForgeList Bmclapi", DlNeoForgeListBmclapiMain);

    /// <summary>
    ///     Cleanroom 版本列表，主加载器。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlCleanroomListResult> DlCleanroomListLoader =
        new("DlCleanroomList Main", DlCleanroomListMain);

    /// <summary>
    ///     Cleanroom 版本列表，官方源。
    /// </summary>
    public static readonly Loading.LoaderTask<int, DlCleanroomListResult>
        DlCleanroomListOfficialLoader =
            new("DlCleanroomList Official", DlCleanroomListOfficialMain);

    private static void DlNeoForgeListMain(Loading.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlNeoForgeListResult>, int>(
                            DlNeoForgeListBmclapiLoader,
                            30),
                        new KeyValuePair<Loading.LoaderTask<int, DlNeoForgeListResult>, int>(
                            DlNeoForgeListOfficialLoader,
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
                        new KeyValuePair<Loading.LoaderTask<int, DlNeoForgeListResult>, int>(
                            DlNeoForgeListOfficialLoader,
                            5),
                        new KeyValuePair<Loading.LoaderTask<int, DlNeoForgeListResult>, int>(
                            DlNeoForgeListBmclapiLoader,
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
                        new KeyValuePair<Loading.LoaderTask<int, DlNeoForgeListResult>, int>(
                            DlNeoForgeListOfficialLoader,
                            60),
                        new KeyValuePair<Loading.LoaderTask<int, DlNeoForgeListResult>, int>(
                            DlNeoForgeListBmclapiLoader,
                            60 + 60)
                    ],
                    loader.isForceRestarting);
                break;
            }
        }
    }

    private static void DlNeoForgeListOfficialMain(
        Loading.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        var resultLatest = Requester
            .FetchJson(
                "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge",
                new RequestParam
                {
                    UseBrowserUserAgent = true
                })
            .ToString();
        var resultLegacy = Requester
            .FetchJson(
                "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge",
                new RequestParam
                {
                    UseBrowserUserAgent = true
                })
            .ToString();
        if (resultLatest.Length < 100 || resultLegacy.Length < 100)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "NeoForge",
                    $"{resultLatest}\r\n\r\n{resultLegacy}"));
        try
        {
            loader.output = new DlNeoForgeListResult
            {
                isOfficial = true,
                sourceName = Lang.Text("Download.Source.NeoForgeOfficial"),
                Value = GetNeoForgeEntries(resultLatest, resultLegacy)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "NeoForge",
                    $"{resultLatest}\r\n\r\n{resultLegacy}"),
                ex);
        }
    }

    public static void DlNeoForgeListBmclapiMain(
        Loading.LoaderTask<int, DlNeoForgeListResult> loader)
    {
        var resultLatest = Requester
            .FetchJson(
                "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge",
                new RequestParam
                {
                    UseBrowserUserAgent = true
                })
            .ToString();
        var resultLegacy = Requester
            .FetchJson(
                "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/forge",
                new RequestParam
                {
                    UseBrowserUserAgent = true
                })
            .ToString();
        if (resultLatest.Length < 100 || resultLegacy.Length < 100)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "NeoForge BMCLAPI",
                    $"{resultLatest}\r\n\r\n{resultLegacy}"));
        try
        {
            loader.output = new DlNeoForgeListResult
            {
                isOfficial = true,
                sourceName = "BMCLAPI",
                Value = GetNeoForgeEntries(resultLatest, resultLegacy)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "NeoForge BMCLAPI",
                    $"{resultLatest}\r\n\r\n{resultLegacy}"),
                ex);
        }
    }

    private static List<DlNeoForgeListEntry> GetNeoForgeEntries(
        string latestJson,
        string latestLegacyJson)
    {
        var versionNames =
            (latestLegacyJson + latestJson).RegexSearch(RegexPatterns.DlNeoForgeVersion);
        var versions = versionNames
            .Where(name => name != "47.1.82")
            .Select(name => new DlNeoForgeListEntry(name))
            .OrderByDescending(a => a)
            .ToList(); // 这个版本虽然在版本列表中，但不能下载
        return versions.Count == 0
            ? throw new NotFoundException()
            : versions;
    }

    private static void DlCleanroomListMain(Loading.LoaderTask<int, DlCleanroomListResult> loader)
    {
        switch (Config.Download.VersionListSource)
        {
            case 0:
            {
                DlSourceLoader(
                    loader,
                    [
                        new KeyValuePair<Loading.LoaderTask<int, DlCleanroomListResult>, int>(
                            DlCleanroomListOfficialLoader,
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
                        new KeyValuePair<Loading.LoaderTask<int, DlCleanroomListResult>, int>(
                            DlCleanroomListOfficialLoader,
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
                        new KeyValuePair<Loading.LoaderTask<int, DlCleanroomListResult>, int>(
                            DlCleanroomListOfficialLoader,
                            60)
                    ],
                    loader.isForceRestarting);
                break;
            }
        }
    }

    private static void DlCleanroomListOfficialMain(
        Loading.LoaderTask<int, DlCleanroomListResult> loader)
    {
        var resultLatest = Requester
            .FetchJson(
                "https://api.github.com/repos/CleanroomMC/Cleanroom/releases",
                new RequestParam
                {
                    UseBrowserUserAgent = true
                })
            .ToString();
        if (resultLatest.Length < 100)
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Cleanroom",
                    resultLatest));
        try
        {
            loader.output = new DlCleanroomListResult
            {
                isOfficial = true,
                sourceName = Lang.Text("Download.Source.CleanroomOfficial"),
                Value = GetCleanroomEntries(resultLatest)
            };
        }
        catch (Exception ex)
        {
            throw new Exception(
                Lang.Text(
                    "Minecraft.Download.Error.VersionListOperationFailed",
                    "Cleanroom",
                    resultLatest),
                ex);
        }
    }

    private static List<DlCleanroomListEntry> GetCleanroomEntries(string latestJson)
    {
        var json = JsonNode.Parse(latestJson)?.AsArray() ?? [];
        var versions = json
            .OfType<JsonObject>()
            .Select(token => new DlCleanroomListEntry(token["tag_name"]?.ToString() ?? "")
            {
                forgeType = (DlForgelikeEntry.ForgelikeType)2
            })
            .ToList();
        if (versions.Count == 0)
            throw new Exception(Lang.Text("Minecraft.Download.Error.NoAvailableVersion"));
        versions = [.. versions.OrderByDescending(a => a.version)];
        return versions;
    }

    public struct DlNeoForgeListResult
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
        ///     所有版本的列表。已经按从新到老排序。
        /// </summary>
        public List<DlNeoForgeListEntry> Value;
    }

    public class DlNeoForgeListEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     API 使用的原始版本字符串，如 “20.4.30-beta”、“1.20.1-47.1.99”（Legacy）。
        /// </summary>
        public readonly string ApiName;

        /// <summary>
        ///     是否是 Beta 版。
        /// </summary>
        public readonly bool IsBeta;

        public DlNeoForgeListEntry(string apiName)
        {
            forgeType = ForgelikeType.NeoForge;
            ApiName = apiName;
            IsBeta = apiName.Contains("beta") || apiName.Contains("alpha");
            if (apiName.Contains("1.20.1"))
            {
                VersionName = apiName.Replace("1.20.1-", "");
                version = new Version($"19.{VersionName}");
                Inherit = "1.20.1";
            }
            else if (apiName.StartsWith("0."))
            {
                VersionName = apiName;
                var segments = apiName.BeforeFirst("-").Split('.');
                version = new Version(0, 0, int.Parse(segments.Last()));
                Inherit = segments[1];
            }
            else
            {
                VersionName = apiName;
                version = new Version(apiName.BeforeFirst("-"));
                Inherit = version.Major >= 24
                    ? $"{version.Major}.{version.Minor}{(version.Build > 0 ? $".{version.Build}" : "")}"
                    : $"1.{version.Major}{(version.Minor > 0 ? $".{version.Minor}" : "")}";
                if (VersionName.Contains('+'))
                    Inherit += $"-{VersionName.AfterFirst("+")}";
            }
        }

        /// <summary>
        ///     文件在官网的基础地址，不包含后缀。
        /// </summary>
        public string UrlBase
        {
            get
            {
                var packageName = IsLegacy ? "forge" : "neoforge";
                return
                    $"https://maven.neoforged.net/releases/net/neoforged/{packageName}/{ApiName}/{packageName}-{ApiName}";
            }
        }
    }

    public struct DlCleanroomListResult
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
        ///     所有版本的列表。已经按从新到老排序。
        /// </summary>
        public List<DlCleanroomListEntry> Value;
    }

    public class DlCleanroomListEntry : DlForgelikeEntry
    {
        /// <summary>
        ///     API 使用的原始版本字符串，如 “0.2.4-alpha”。
        /// </summary>
        public readonly string ApiName;

        /// <summary>
        ///     是否是 Beta 版。
        /// </summary>
        public readonly bool IsBeta;

        public DlCleanroomListEntry(string apiName)
        {
            forgeType = ForgelikeType.Cleanroom;
            ApiName = apiName;
            IsBeta = apiName.Contains("alpha");
            VersionName = apiName;
            version = new Version(apiName.BeforeFirst("-"));
            Inherit = "1.12.2";
        }

        /// <summary>
        ///     文件在官网的基础地址，不包含后缀。
        /// </summary>
        public string UrlBase =>
            $"https://github.com/CleanroomMC/Cleanroom/releases/download/{ApiName}/cleanroom-{ApiName}";
    }
}