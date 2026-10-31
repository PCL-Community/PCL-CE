using System.Net;
using System.Text;
using PCL.Core.App.Localization;
using PCL.Core.Utils;
using PCL.Network;

namespace PCL;

public static partial class Comp
{
    public enum CompFileStatus
    {
        Release = 1,
        Beta = 2,
        Alpha = 3
    }

    public class CompFile
    {
        /// <summary>
        ///     无法识别版本时使用的占位标记。该值语言中立，显示时请通过 <see cref="Comp.FormatGameVersion" /> 求值。
        /// </summary>
        internal const string UnknownGameVersion = "\0unknown";

        /// <summary>
        ///     远古版本（如 b1.x）使用的占位标记。该值语言中立，显示时请通过 <see cref="Comp.FormatGameVersion" /> 求值。
        /// </summary>
        internal const string AncientGameVersion = "\0ancient";

        /// <summary>
        ///     预发布版本在 <see cref="GameVersions" /> 中的后缀标记。该值语言中立，显示时请通过 <see cref="Comp.FormatGameVersion" />
        ///     求值。
        /// </summary>
        internal const string PreviewVersionSuffix = "-pre";

        /// <summary>
        ///     该文件的所有必要依赖工程的 Project.Id。
        /// </summary>
        public readonly List<string> Dependencies;

        /// <summary>
        ///     下载量计数。注意，该计数仅为一个来源，无法反应两边加起来的下载量，且 CurseForge 可能错误地返回 0。
        /// </summary>
        public readonly int DownloadCount;

        /// <summary>
        ///     下载的文件名。
        /// </summary>
        public readonly string FileName;

        /// <summary>
        ///     该文件来自 CurseForge 还是 Modrinth。
        /// </summary>
        public readonly bool FromCurseForge;

        /// <summary>
        ///     支持的游戏版本列表。<br />
        ///     该列表只保存语言中立的原始版本标识（例如 "26.1.5"、"1.18.5"、"1.18-pre1"、"21w15a"），
        ///     以及无法识别时的 <see cref="UnknownGameVersion" />；
        ///     需要展示给用户时请通过 <see cref="Comp.FormatGameVersion" /> 按当前语言求值。
        /// </summary>
        public readonly List<string> GameVersions;

        /// <summary>
        ///     文件的 SHA1 或 MD5。
        /// </summary>
        public readonly string Hash;

        /// <summary>
        ///     用于唯一性鉴别该文件的 ID。CurseForge 中为 123456 的大整数，Modrinth 中为英文乱码的 Version 字段。
        /// </summary>
        public readonly string Id;

        /// <summary>
        ///     支持的 Mod 加载器列表。可能为空。
        /// </summary>
        public readonly List<CompLoaderType> ModLoaders;

        /// <summary>
        ///     该文件的所有可选依赖工程的 Project.Id。
        /// </summary>
        public readonly List<string> OptionalDependencies;

        /// <summary>
        ///     该文件所属项目的 ID。
        /// </summary>
        public readonly string ProjectId;

        /// <summary>
        ///     该文件的所有必要依赖工程的原始 ID。
        ///     这些 ID 可能没有加载，在加载后会添加到 Dependencies 中（主要是因为 Modrinth 返回的是字符串 ID 而非 Slug，导致 Project.Id 查询不到）。
        /// </summary>
        public readonly List<string> RawDependencies;

        /// <summary>
        ///     未经处理的支持的游戏版本列表。
        /// </summary>
        public readonly List<string> RawGameVersions;

        /// <summary>
        ///     该文件的所有可选依赖工程的原始 ID。
        ///     这些 ID 可能没有加载，在加载后会添加到 OptionalDependencies 中（主要是因为 Modrinth 返回的是字符串 ID 而非 Slug，导致 Project.Id
        ///     查询不到）。
        /// </summary>
        public readonly List<string> RawOptionalDependencies;

        /// <summary>
        ///     发布时间。
        /// </summary>
        public readonly DateTime ReleaseDate;

        /// <summary>
        ///     发布状态：Release/Beta/Alpha。
        /// </summary>
        public readonly CompFileStatus Status;

        /// <summary>
        ///     文件的种类。
        /// </summary>
        public readonly CompType Type;

        /// <summary>
        ///     文件描述名（并非文件名，是自定义的字段）。对很多 Mod，这会给出 Mod 版本号。
        /// </summary>
        public string DisplayName;

        /// <summary>
        ///     文件所有可能的下载源。
        /// </summary>
        public List<string> DownloadUrls;

        /// <summary>
        ///     Mod 版本号。
        ///     不一定是标准格式。CurseForge 上默认为 Nothing。
        /// </summary>
        public string Version;

        /// <summary>
        ///     从文件 Json 中初始化实例。若出错会抛出异常。
        /// </summary>
        public CompFile(JsonObject data, CompType defaultType)
        {
            CompFileBuildResult result;

            if (data.ContainsKey("FromCurseForge"))
                result = _BuildFromCompJson(data, defaultType);
            else if (data.ContainsKey("gameId"))
                result = _BuildFromCurseForge(data, defaultType);
            else
                result = _BuildFromModrinth(data, defaultType);

            FromCurseForge = result.FromCurseForge;
            Type = result.Type;
            Id = result.Id;
            ProjectId = result.ProjectId ?? "";
            DisplayName = result.DisplayName;
            Version = result.Version ?? "";
            ReleaseDate = result.ReleaseDate;
            Status = result.Status;
            DownloadCount = result.DownloadCount;
            FileName = result.FileName ?? "";
            Hash = result.Hash ?? "";
            DownloadUrls = result.DownloadUrls ?? [];
            ModLoaders = result.ModLoaders;
            RawGameVersions = result.RawGameVersions;
            GameVersions = result.GameVersions;
            RawDependencies = result.RawDependencies;
            Dependencies = result.Dependencies;
            RawOptionalDependencies = result.RawOptionalDependencies;
            OptionalDependencies = result.OptionalDependencies;
        }

        /// <summary>
        ///     发布状态的友好描述。例如："正式版"，"Beta 版"。
        /// </summary>

        public string StatusDescription =>
            Status switch
            {
                CompFileStatus.Release => Lang.Text(
                    "Download.Comp.Detail.FileList.ReleaseType.Release"),
                CompFileStatus.Beta => Lang.Text(
                    "Download.Comp.Detail.FileList.ReleaseType.Beta"),
                _ => Lang.Text(
                    "Download.Comp.Detail.FileList.ReleaseType.Alpha")
            };

        /// <summary>
        ///     下载信息是否可用。
        /// </summary>
        public bool Available => !string.IsNullOrEmpty(FileName) && DownloadUrls.Count != 0;

        private static CompFileBuildResult _BuildFromCompJson(JsonObject data, CompType defaultType)
        {
            var result = new CompFileBuildResult
            {
                Type = defaultType,
                FromCurseForge = data["FromCurseForge"]?.ToObject<bool>() ?? false,
                Id = data["Id"]?.ToString() ?? "",
                DisplayName = data["DisplayName"]?.ToString() ?? "",
                ReleaseDate = data["ReleaseDate"]?.ToObject<DateTime>() ?? default,
                DownloadCount = data["DownloadCount"]?.ToObject<int>() ?? 0,
                Status = (CompFileStatus)(data["Status"]?.ToObject<int>() ?? 0)
            };

            if (data.ContainsKey("Version"))
                result.Version = data["Version"]?.ToString();
            if (data.ContainsKey("FileName"))
                result.FileName = data["FileName"]?.ToString();
            if (data.ContainsKey("DownloadUrls"))
                result.DownloadUrls = data["DownloadUrls"]?.ToObject<List<string>>();
            if (data.ContainsKey("ModLoaders"))
                result.ModLoaders = data["ModLoaders"]?.ToObject<List<CompLoaderType>>() ?? [];
            if (data.ContainsKey("Hash"))
                result.Hash = data["Hash"]?.ToString();
            if (data.ContainsKey("RawGameVersions"))
                result.RawGameVersions = data["RawGameVersions"]?.ToObject<List<string>>() ?? [];
            if (data.ContainsKey("GameVersions"))
                result.GameVersions = data["GameVersions"]?.ToObject<List<string>>() ?? [];
            if (data.ContainsKey("RawDependencies"))
                result.RawDependencies = data["RawDependencies"]?.ToObject<List<string>>() ?? [];
            if (data.ContainsKey("Dependencies"))
                result.Dependencies = data["Dependencies"]?.ToObject<List<string>>() ?? [];
            if (data.ContainsKey("RawOptionalDependencies"))
                result.RawOptionalDependencies =
                    data["RawOptionalDependencies"]?.ToObject<List<string>>() ?? [];
            if (data.ContainsKey("OptionalDependencies"))
                result.OptionalDependencies =
                    data["OptionalDependencies"]?.ToObject<List<string>>() ?? [];

            return result;
        }

        private static CompFileBuildResult _BuildFromCurseForge(
            JsonObject data,
            CompType defaultType)
        {
            var hashes = data["hashes"]?.AsArray() ?? [];
            var result = new CompFileBuildResult
            {
                FromCurseForge = true,
                Type = defaultType,
                Id = data["id"]?.ToString() ?? "",
                ProjectId = data["modId"]?.ToString(),
                DisplayName = NormalizeDisplayName(data["displayName"]?.ToString() ?? ""),
                ReleaseDate = data["fileDate"]?.ToObject<DateTime>() ?? default,
                Status = (CompFileStatus)(data["releaseType"]?.ToObject<int>() ?? 0),
                DownloadCount = data["downloadCount"]?.ToObject<int>() ?? 0,
                FileName = (string?)data["fileName"],
                Hash = GetCurseForgeHash(hashes, 1) ?? GetCurseForgeHash(hashes, 2)
            };

            result.DownloadUrls = BuildCurseForgeDownloadUrls(data, result);

            if (data.ContainsKey("dependencies"))
            {
                var dependencies = data["dependencies"]?.AsArray() ?? [];
                result.RawDependencies = BuildCurseForgeDependencies(dependencies, 3);
                result.RawOptionalDependencies = BuildCurseForgeDependencies(dependencies, 2);
            }

            result.RawGameVersions =
            [
                .. data["gameVersions"]
                    ?.AsArray()
                    .Select(version => version?.ToString()?.Trim().ToLower() ?? "")
                ?? []
            ];
            result.GameVersions = BuildCurseForgeGameVersions(
                result.RawGameVersions,
                result.Type);
            result.ModLoaders = BuildCurseForgeModLoaders(result.RawGameVersions);

            return result;
        }

        private static string NormalizeDisplayName(string displayName)
        {
            return displayName.Replace("\t", "").Trim(' ');
        }

        private static string? GetCurseForgeHash(JsonArray hashes, int algorithm)
        {
            return hashes
                .ToList()
                .FirstOrDefault(hash => hash?["algo"]?.ToObject<int>() == algorithm)?["value"]
                ?.ToString();
        }

        private static List<string> BuildCurseForgeDownloadUrls(
            JsonObject data,
            CompFileBuildResult result)
        {
            var url = data["downloadUrl"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(url))
                url =
                    $"https://edge.forgecdn.net/files/{int.Parse(result.Id[..4])}/{int.Parse(result.Id[4..])}/{result.FileName}";

            if (!string.IsNullOrEmpty(result.FileName))
                url = url.Replace(result.FileName, WebUtility.UrlEncode(result.FileName));
            url = url.Replace("+", "%20");

            return GameDownload.DlSourceModDownloadGet(
                HandleCurseForgeDownloadUrls(url));
        }

        private static List<string> BuildCurseForgeDependencies(
            JsonArray dependencies,
            int relationType)
        {
            return
            [
                .. dependencies
                    .Where(dependency =>
                        dependency?["relationType"]?.ToObject<int>() == relationType
                        && dependency?["modId"]?.ToObject<int>() != 306612
                        && dependency?["modId"]?.ToObject<int>() != 634179)
                    .Select(dependency => dependency?["modId"]?.ToString() ?? "")
            ];
        }

        private static List<string> BuildCurseForgeGameVersions(
            List<string> rawGameVersions,
            CompType type)
        {
            var gameVersions = rawGameVersions
                .Where(McInstanceInfo.IsFormatFit)
                .Select(NormalizeGameVersion)
                .Distinct()
                .ToList();

            switch (gameVersions.Count)
            {
                case > 1:
                {
                    gameVersions =
                    [
                        .. LauncherText.SortByPredicate(
                            gameVersions,
                            McVersionComparer.CompareVersionGe)
                    ];
                    if (type == CompType.ModPack)
                        gameVersions = [gameVersions[0]];
                    break;
                }
                case 1:
                    gameVersions = [.. gameVersions];
                    break;
                default:
                    gameVersions = [UnknownGameVersion];
                    break;
            }

            return gameVersions;
        }

        private static List<CompLoaderType> BuildCurseForgeModLoaders(
            List<string> rawGameVersions)
        {
            var modLoaders = new List<CompLoaderType>();

            if (rawGameVersions.Contains("forge"))
                modLoaders.Add(CompLoaderType.Forge);
            if (rawGameVersions.Contains("fabric"))
                modLoaders.Add(CompLoaderType.Fabric);
            if (rawGameVersions.Contains("quilt"))
                modLoaders.Add(CompLoaderType.Quilt);
            if (rawGameVersions.Contains("neoforge"))
                modLoaders.Add(CompLoaderType.NeoForge);

            return modLoaders;
        }

        private static CompFileBuildResult _BuildFromModrinth(
            JsonObject data,
            CompType defaultType)
        {
            var result = new CompFileBuildResult
            {
                FromCurseForge = false,
                Type = defaultType,
                Id = (string?)data["id"] ?? "",
                ProjectId = (string?)data["project_id"],
                DisplayName = NormalizeDisplayName(data["name"]?.ToString() ?? ""),
                Version = (string?)data["version_number"],
                ReleaseDate = data["date_published"]?.ToObject<DateTime>() ?? default,
                Status = GetModrinthStatus(data["version_type"]?.ToString() ?? ""),
                DownloadCount = data["downloads"]?.ToObject<int>() ?? 0
            };

            var files = data["files"]?.AsArray() ?? [];
            if (files.Count != 0)
            {
                var file = files[0];
                if (file is not null)
                {
                    result.FileName = (string?)file["filename"];
                    result.DownloadUrls =
                        GameDownload.DlSourceModDownloadGet(file["url"]?.ToString() ?? "");
                    result.Hash = (string?)file["hashes"]?["sha1"];
                }
            }

            var rawLoaders = (data["loaders"]?.AsArray() ?? [])
                .Select(loader => loader?.ToString() ?? "")
                .ToList();
            BuildModrinthLoaderInfo(result, rawLoaders);

            if (data.ContainsKey("dependencies"))
            {
                var dependencies = data["dependencies"]?.AsArray() ?? [];
                result.RawDependencies =
                    BuildModrinthDependencies(dependencies, "required");
                result.RawOptionalDependencies =
                    BuildModrinthDependencies(dependencies, "optional");
            }

            result.RawGameVersions =
            [
                .. data["game_versions"]
                    ?.AsArray()
                    .Select(version => version?.ToString()?.Trim().ToLower() ?? "")
                ?? []
            ];
            result.GameVersions = BuildModrinthGameVersions(
                result.RawGameVersions,
                result.Type);

            return result;
        }

        private static CompFileStatus GetModrinthStatus(string versionType)
        {
            return versionType switch
            {
                "release" => CompFileStatus.Release,
                "beta" => CompFileStatus.Beta,
                _ => CompFileStatus.Alpha
            };
        }

        private static void BuildModrinthLoaderInfo(
            CompFileBuildResult result,
            List<string> rawLoaders)
        {
            result.ModLoaders = [];

            var originalType = result.Type;
            if (originalType != CompType.Mod && originalType != CompType.DataPack)
                return;

            if (rawLoaders
                .Intersect(["bukkit", "folia", "paper", "purpur", "spigot"])
                .Any())
                result.Type = CompType.Plugin;

            if (originalType == CompType.Mod && rawLoaders.Contains("datapack"))
                result.Type = CompType.DataPack;

            AddLoader("forge", CompLoaderType.Forge);
            AddLoader("neoforge", CompLoaderType.NeoForge);
            AddLoader("fabric", CompLoaderType.Fabric);
            AddLoader("quilt", CompLoaderType.Quilt);

            if (originalType == CompType.DataPack && rawLoaders.Contains("datapack"))
                result.Type = CompType.DataPack;
            return;

            void AddLoader(string loaderName, CompLoaderType loaderType)
            {
                if (!rawLoaders.Contains(loaderName))
                    return;

                result.ModLoaders.Add(loaderType);
                result.Type = CompType.Mod;
            }
        }

        private static List<string> BuildModrinthDependencies(
            JsonArray dependencies,
            string dependencyType)
        {
            return
            [
                .. dependencies
                    .Where(dependency =>
                        (string?)dependency?["dependency_type"] == dependencyType
                        && dependency?["project_id"] is not null
                        && (string?)dependency?["project_id"] != "P7dR8mSH"
                        && (string?)dependency?["project_id"] != "qvIfYCYJ")
                    .Select(dependency => dependency?["project_id"]?.ToString() ?? "")
            ];
        }

        private static List<string> BuildModrinthGameVersions(
            List<string> rawGameVersions,
            CompType type)
        {
            var gameVersions = rawGameVersions
                .Where(version => version.Contains('.'))
                .Select(NormalizeGameVersion)
                .Distinct()
                .ToList();

            switch (gameVersions.Count)
            {
                case > 1:
                {
                    gameVersions =
                    [
                        .. LauncherText.SortByPredicate(
                            gameVersions,
                            McVersionComparer.CompareVersionGe)
                    ];
                    if (type == CompType.ModPack)
                        gameVersions = [gameVersions[0]];
                    break;
                }
                case 0:
                {
                    var snapshots = rawGameVersions
                        .Where(version => version.RegexCheck("[0-9]{2}w[0-9]{2}[a-z]"))
                        .ToList();
                    gameVersions = snapshots.Count > 0
                        ? snapshots
                        : [UnknownGameVersion];
                    break;
                }
            }

            return gameVersions;
        }

        /// <summary>
        ///     获取下载信息。
        /// </summary>
        /// <param name="localAddress">目标本地文件夹，或完整的文件路径。会自动判断类型。</param>
        /// <param name="reason">下载原因。</param>
        /// <returns>下载信息。</returns>
        public DownloadFile ToNetFile(
            string localAddress,
            DownloadReason reason = DownloadReason.Standalone)
        {
            return ToNetFile(
                localAddress,
                reason,
                RawGameVersions.FirstOrDefault(),
                ModLoaders.FirstOrDefault());
        }

        /// <summary>
        ///     获取下载信息。
        /// </summary>
        /// <param name="localAddress">目标本地文件夹，或完整的文件路径。会自动判断类型。</param>
        /// <param name="reason">下载原因。</param>
        /// <param name="version">实例版本。</param>
        /// <param name="modLoader">实例的模组加载器。</param>
        /// <returns>下载信息。</returns>
        public DownloadFile ToNetFile(
            string localAddress,
            DownloadReason reason,
            string? version,
            CompLoaderType modLoader = CompLoaderType.Any)
        {
            if (DownloadUrls is null) throw new InvalidCastException("DownloadUrls 为空");

            return new DownloadFile(
                HandleModrinthDownloadUrls(DownloadUrls, reason, version, modLoader),
                localAddress + (localAddress.EndsWithF(@"\") ? CompFileNameSanitize(FileName) : ""),
                new FileChecker(hash: Hash),
                true);
        }

        /// <summary>
        ///     对之前错误的 CurseForge 的下载地址进行修正。
        /// </summary>
        public static string HandleCurseForgeDownloadUrls(string url)
        {
            return url
                .Replace("-service.overwolf.wtf", ".forgecdn.net")
                .Replace("://media.", "://edge.")
                .Replace("://mediafilez.", "://edge.");
        }

        /// <summary>
        ///     对 Modrinth 下载链接添加上报参数。
        /// </summary>
        /// <param name="urls">下载链接。</param>
        /// <param name="reason">下载原因。</param>
        /// <param name="version">实例版本。</param>
        /// <param name="modLoader">实例的模组加载器。</param>
        /// <returns>处理后的下载链接。</returns>
        public static IEnumerable<string> HandleModrinthDownloadUrls(
            IEnumerable<string> urls,
            DownloadReason reason = DownloadReason.Standalone,
            string? version = null,
            CompLoaderType modLoader = CompLoaderType.Any)
        {
            foreach (var url in urls)
            {
                if (!url.Contains("modrinth", StringComparison.InvariantCultureIgnoreCase))
                {
                    yield return url;
                    continue;
                }

                var sb = new StringBuilder(url);

                sb.Append(url.Contains('?') ? "&mr_download_reason=" : "?mr_download_reason=");
                sb.Append(reason.ToString().ToLowerInvariant());

                if (version is not null)
                {
                    sb.Append("&mr_game_version=");
                    sb.Append(version);
                }

                if (modLoader != CompLoaderType.Any)
                {
                    sb.Append("&mr_loader=");
                    sb.Append(modLoader.ToString().ToLowerInvariant());
                }

                yield return sb.ToString();
            }
        }

        /// <summary>
        ///     将当前实例转为可用于保存缓存的 Json。
        /// </summary>
        public JsonObject ToJson()
        {
            var json = new JsonObject
            {
                {
                    "FromCurseForge", FromCurseForge
                },
                {
                    "Id", Id
                }
            };
            if (Version is not null)
                json.Add("Version", Version);

            json.Add("DisplayName", DisplayName);
            json.Add("ReleaseDate", ReleaseDate);
            json.Add("DownloadCount", DownloadCount);
            json.Add(
                "ModLoaders",
                new JsonArray(ModLoaders.Select(m => (JsonNode)(int)m).ToArray()));
            json.Add(
                "RawGameVersions",
                new JsonArray(RawGameVersions.Select(s => (JsonNode)s).ToArray()));
            json.Add(
                "GameVersions",
                new JsonArray(GameVersions.Select(s => (JsonNode)s).ToArray()));
            json.Add("Status", (int)Status);

            if (FileName is not null)
                json.Add("FileName", FileName);

            if (DownloadUrls is not null)
                json.Add(
                    "DownloadUrls",
                    new JsonArray(DownloadUrls.Select(s => (JsonNode)s).ToArray()));

            if (Hash is not null)
                json.Add("Hash", Hash);

            json.Add(
                "RawDependencies",
                new JsonArray(RawDependencies.Select(s => (JsonNode)s).ToArray()));
            json.Add(
                "RawOptionalDependencies",
                new JsonArray(RawOptionalDependencies.Select(s => (JsonNode)s).ToArray()));
            json.Add(
                "Dependencies",
                new JsonArray(Dependencies.Select(s => (JsonNode)s).ToArray()));
            json.Add(
                "OptionalDependencies",
                new JsonArray(OptionalDependencies.Select(s => (JsonNode)s).ToArray()));

            return json;
        }

        public override string ToString()
        {
            return $"{Id}: {FileName}";
        }

        private sealed class CompFileBuildResult
        {
            public List<string> Dependencies = [];
            public string DisplayName = "";
            public int DownloadCount;
            public List<string>? DownloadUrls;
            public string? FileName;
            public bool FromCurseForge;
            public List<string> GameVersions = [];
            public string? Hash;
            public string Id = "";
            public List<CompLoaderType> ModLoaders = [];
            public List<string> OptionalDependencies = [];
            public string? ProjectId;
            public List<string> RawDependencies = [];
            public List<string> RawGameVersions = [];
            public List<string> RawOptionalDependencies = [];
            public DateTime ReleaseDate;
            public CompFileStatus Status;
            public CompType Type;
            public string? Version;
        }
    }
}