using PCL.Core.Utils;

namespace PCL;

public static partial class Comp
{
    public partial class CompProject
    {
        private static readonly Dictionary<string, CompLoaderType> ModrinthLoaderTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["forge"] = CompLoaderType.Forge,
                ["fabric"] = CompLoaderType.Fabric,
                ["quilt"] = CompLoaderType.Quilt,
                ["neoforge"] = CompLoaderType.NeoForge
            };

        private static readonly Dictionary<int, string> CurseForgeCategoryLangKeys =
            new()
            {
                [406] = "Download.Comp.Category.WorldGen",
                [407] = "Download.Comp.Category.Biomes",
                [410] = "Download.Comp.Category.Dimensions",
                [408] = "Download.Comp.Category.OresResources",
                [409] = "Download.Comp.Category.Structures",
                [412] = "Download.Comp.Category.Technology",
                [415] = "Download.Comp.Category.PipesLogistics",
                [4843] = "Download.Comp.Category.Automation",
                [417] = "Download.Comp.Category.Energy",
                [4558] = "Download.Comp.Category.Redstone",
                [436] = "Download.Comp.Category.FoodCooking",
                [416] = "Download.Comp.Category.Farming",
                [414] = "Download.Comp.Category.Transportation",
                [420] = "Download.Comp.Category.Storage",
                [419] = "Download.Comp.Category.Magic",
                [422] = "Download.Comp.Category.Adventure",
                [424] = "Download.Comp.Category.Decoration",
                [411] = "Download.Comp.Category.Mobs",
                [434] = "Download.Comp.Category.Equipment",
                [6814] = "Download.Comp.Category.Optimization",
                [9026] = "Download.Comp.Category.Creative",
                [423] = "Download.Comp.Category.Display",
                [435] = "Download.Comp.Category.Server",
                [5191] = "Download.Comp.Category.Tweaks",
                [421] = "Download.Comp.Category.Library",

                [4484] = "Download.Comp.Category.Multiplayer",
                [4479] = "Download.Comp.Category.Modpack.Hardcore",
                [4483] = "Download.Comp.Category.Combat",
                [4478] = "Download.Comp.Category.Modpack.Quests",
                [4472] = "Download.Comp.Category.Technology",
                [4473] = "Download.Comp.Category.Magic",
                [4475] = "Download.Comp.Category.Adventure",
                [4476] = "Download.Comp.Category.Modpack.Exploration",
                [4477] = "Download.Comp.Category.Modpack.MiniGame",
                [4471] = "Download.Comp.Category.Modpack.SciFi",
                [4736] = "Download.Comp.Category.Modpack.Skyblock",
                [5128] = "Download.Comp.Category.Modpack.VanillaPlus",
                [4487] = "Download.Comp.Category.Modpack.Ftb",
                [4480] = "Download.Comp.Category.Modpack.MapBased",
                [4481] = "Download.Comp.Category.Modpack.SmallLight",
                [4482] = "Download.Comp.Category.Modpack.ExtraLarge",

                [403] = "Download.Comp.Category.VanillaLike",
                [400] = "Download.Comp.Category.Realistic",
                [401] = "Download.Comp.Category.Modern",
                [402] = "Download.Comp.Category.Medieval",
                [399] = "Download.Comp.Category.Steampunk",
                [5244] = "Download.Comp.Category.Fonts",
                [404] = "Download.Comp.Category.Animated",
                [4465] = "Download.Comp.Category.ModSupport",
                [393] = "Download.Comp.Category.ResourcePack.Resolution16x",
                [394] = "Download.Comp.Category.ResourcePack.Resolution32x",
                [395] = "Download.Comp.Category.ResourcePack.Resolution64x",
                [396] = "Download.Comp.Category.ResourcePack.Resolution128x",
                [397] = "Download.Comp.Category.ResourcePack.Resolution256x",
                [398] = "Download.Comp.Category.ResourcePack.Resolution512xOrHigher",
                [5193] = "Download.Comp.Type.DataPack",

                [6553] = "Download.Comp.Category.Realistic",
                [6554] = "Download.Comp.Category.Fantasy",
                [6555] = "Download.Comp.Category.VanillaLike",

                [6948] = "Download.Comp.Category.Adventure",
                [6949] = "Download.Comp.Category.DataPack.Fantasy",
                [6950] = "Download.Comp.Category.Library",
                [6952] = "Download.Comp.Category.Magic",
                [6946] = "Download.Comp.Category.Mod.ModRelated",
                [6951] = "Download.Comp.Category.Technology",
                [6953] = "Download.Comp.Category.Utility",

                [248] = "Download.Comp.Category.Adventure",
                [249] = "Download.Comp.Category.World.Creative",
                [250] = "Download.Comp.Category.Modpack.MiniGame",
                [251] = "Download.Comp.Category.World.Parkour",
                [252] = "Download.Comp.Category.World.Puzzle",
                [253] = "Download.Comp.Category.World.Survival",
                [4464] = "Download.Comp.Category.World.ModWorld"
            };

        private static readonly Dictionary<string, string> ResourcePackHiddenCategoryLangKeys =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["decoration"] = "Download.Comp.Category.Decoration",
                ["mobs"] = "Download.Comp.Category.Mobs",
                ["equipment"] = "Download.Comp.Category.Equipment"
            };

        private static readonly Dictionary<string, string> ModrinthCategoryLangKeys =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["technology"] = "Download.Comp.Category.Technology",
                ["magic"] = "Download.Comp.Category.Magic",
                ["adventure"] = "Download.Comp.Category.Adventure",
                ["utility"] = "Download.Comp.Category.Utility",
                ["optimization"] = "Download.Comp.Category.Optimization",
                ["vanilla-like"] = "Download.Comp.Category.VanillaLike",
                ["realistic"] = "Download.Comp.Category.Realistic",

                ["worldgen"] = "Download.Comp.Category.WorldGen",
                ["food"] = "Download.Comp.Category.FoodCooking",
                ["game-mechanics"] = "Download.Comp.Category.GameMechanics",
                ["transportation"] = "Download.Comp.Category.Transportation",
                ["storage"] = "Download.Comp.Category.Storage",
                ["social"] = "Download.Comp.Category.Server",
                ["library"] = "Download.Comp.Category.Library",

                ["multiplayer"] = "Download.Comp.Category.Multiplayer",
                ["challenging"] = "Download.Comp.Category.Modpack.Hardcore",
                ["combat"] = "Download.Comp.Category.Combat",
                ["quests"] = "Download.Comp.Category.Modpack.Quests",
                ["kitchen-sink"] = "Download.Comp.Category.Modpack.KitchenSink",
                ["lightweight"] = "Download.Comp.Category.Modpack.SmallLight",

                ["simplistic"] = "Download.Comp.Category.Simplistic",
                ["tweaks"] = "Download.Comp.Category.Tweaks",
                ["8x-"] = "Download.Comp.Category.ResourcePack.Resolution8xOrLower",
                ["16x"] = "Download.Comp.Category.ResourcePack.Resolution16x",
                ["32x"] = "Download.Comp.Category.ResourcePack.Resolution32x",
                ["48x"] = "Download.Comp.Category.ResourcePack.Resolution48x",
                ["64x"] = "Download.Comp.Category.ResourcePack.Resolution64x",
                ["128x"] = "Download.Comp.Category.ResourcePack.Resolution128x",
                ["256x"] = "Download.Comp.Category.ResourcePack.Resolution256x",
                ["512x+"] = "Download.Comp.Category.ResourcePack.Resolution512xOrHigher",
                ["audio"] = "Download.Comp.Category.Audio",
                ["fonts"] = "Download.Comp.Category.Fonts",
                ["models"] = "Download.Comp.Category.Models",
                ["gui"] = "Download.Comp.Category.Gui",
                ["locale"] = "Download.Comp.Category.Locale",
                ["core-shaders"] = "Download.Comp.Category.CoreShaders",
                ["modded"] = "Download.Comp.Category.ModSupport",

                ["fantasy"] = "Download.Comp.Category.Fantasy",
                ["semi-realistic"] = "Download.Comp.Category.SemiRealistic",
                ["cartoon"] = "Download.Comp.Category.Cartoon",
                ["colored-lighting"] = "Download.Comp.Category.ColoredLighting",
                ["path-tracing"] = "Download.Comp.Category.PathTracing",
                ["pbr"] = "Download.Comp.Category.Pbr",
                ["reflections"] = "Download.Comp.Category.Reflections",
                ["iris"] = "Download.Comp.Category.Iris",
                ["optifine"] = "Download.Comp.Category.Optifine",
                ["vanilla"] = "Download.Comp.Filter.Loader.VanillaAvailable"
            };

        private static CompProjectBuildResult _BuildFromCompJson(JsonObject data)
        {
            var result = new CompProjectBuildResult
            {
                FromCurseForge = (string?)data["DataSource"] == "CurseForge",
                Type = (CompType)(data["Type"]?.ToObject<int>() ?? 0),
                Slug = (string?)data["Slug"] ?? "",
                Id = (string?)data["Id"] ?? "",
                RawName = (string?)data["RawName"] ?? "",
                Description = (string?)data["Description"] ?? "",
                Website = (string?)data["Website"] ?? "",
                DownloadCount = data["DownloadCount"]?.ToObject<int>() ?? 0,
                Tags =
                [
                    .. (data["Tags"] as JsonArray ?? [])
                    .Select(t => t?.ToString() ?? "")
                ]
            };

            if (data.TryGetPropertyValue("CurseForgeFileIds", out var id))
                result.CurseForgeFileIds =
                [
                    .. (id as JsonArray ?? [])
                    .Select(t => t?.ToObject<int>() ?? 0)
                ];

            if (data.TryGetPropertyValue("LastUpdate", out var last))
                result.LastUpdate = last?.ToObject<DateTime>();

            if (data.TryGetPropertyValue("ModLoaders", out var loaders))
                result.ModLoaders =
                [
                    .. (loaders as JsonArray ?? [])
                    .Select(t => (CompLoaderType)(t?.ToObject<int>() ?? 0))
                ];

            if (data.TryGetPropertyValue("LogoUrl", out var url))
                result.LogoUrl = (string?)url;

            if (data.TryGetPropertyValue("Drops", out var drops))
                result.Drops =
                [
                    .. (drops as JsonArray ?? [])
                    .Select(t => t?.ToObject<int>() ?? 0)
                ];

            return result;
        }

        private static CompProjectBuildResult _BuildFromCurseForge(JsonObject data)
        {
            var result = new CompProjectBuildResult
            {
                FromCurseForge = true,

                Id = data["id"]?.ToString() ?? "",
                Slug = (string?)data["slug"] ?? "",
                RawName = (string?)data["name"] ?? "",
                Description = (string?)data["summary"] ?? "",
                Website = (data["links"]?["websiteUrl"]?.ToString() ?? "").TrimEnd('/'),
                LastUpdate = data["dateReleased"]?.ToObject<DateTime>(),
                DownloadCount = data["downloadCount"]?.ToObject<int>() ?? 0
            };

            if (data["logo"] is JsonObject { Count: > 0 } logo)
                result.LogoUrl = string.IsNullOrEmpty((string?)logo["thumbnailUrl"])
                    ? (string?)logo["url"]
                    : (string?)logo["thumbnailUrl"];

            if (string.IsNullOrEmpty(result.LogoUrl))
                result.LogoUrl = null;

            result.Type = _GetCurseForgeTypeByWebsite(result.Website);

            var files = new List<KeyValuePair<int, List<string>>>();

            foreach (var file in data["latestFiles"] as JsonArray ?? [])
            {
                if (file is not JsonObject fileJson) continue;
                var newFile = new CompFile(fileJson, result.Type);
                if (!newFile.Available)
                    continue;

                result.ModLoaders.AddRange(newFile.ModLoaders);

                var gameVersions = fileJson["gameVersions"]?.ToObject<List<string>>() ?? [];
                if (!gameVersions.Any(McInstanceInfo.IsFormatFit))
                    continue;

                files.Add(
                    new KeyValuePair<int, List<string>>(
                        fileJson["id"]?.ToObject<int>() ?? 0,
                        gameVersions));
            }

            files.AddRange(
                from File in data["latestFilesIndexes"] as JsonArray ?? []
                let GameVersion = File?["gameVersion"]?.ToString() ?? ""
                where McInstanceInfo.IsFormatFit(GameVersion)
                select new KeyValuePair<int, List<string>>(
                    File?["fileId"]?.ToObject<int>() ?? 0,
                    [GameVersion])
            );

            result.CurseForgeFileIds =
            [
                .. files
                    .Select(f => f.Key)
                    .Distinct()
            ];

            result.Drops =
            [
                .. files
                    .SelectMany(f => f.Value)
                    .Select(v => McInstanceInfo.VersionToDrop(v))
                    .Where(v => v > 0)
                    .Distinct()
                    .OrderByDescending(v => v)
            ];

            result.ModLoaders =
            [
                .. result
                    .ModLoaders
                    .Distinct()
                    .OrderBy(t => t)
            ];

            var categories = (data["categories"] as JsonArray ?? [])
                .Select(t => t?["id"]?.ToObject<int?>())
                .Where(t => t.HasValue)
                .Select(t => t.GetValueOrDefault())
                .Distinct()
                .OrderByDescending(t => t);

            foreach (var category in categories)
                if (CurseForgeCategoryLangKeys.TryGetValue(category, out var langKey))
                    _AddTag(result.Tags, langKey);

            return result;
        }

        private static CompProjectBuildResult _BuildFromModrinth(JsonObject data)
        {
            var projectType = data["project_type"]?.ToString() ?? "";
            var slug = (string?)data["slug"] ?? "";

            var result = new CompProjectBuildResult
            {
                FromCurseForge = false,

                Id = (string?)(data["project_id"] ?? data["id"]) ?? "",
                Slug = slug,
                RawName = (string?)data["title"] ?? "",
                Description = (string?)data["description"] ?? "",
                LastUpdate = data["date_modified"]?.ToObject<DateTime>(),
                DownloadCount = data["downloads"]?.ToObject<int>() ?? 0,
                LogoUrl = (string?)data["icon_url"],
                Website = $"https://modrinth.com/{projectType}/{slug}",

                Type = projectType switch
                {
                    "modpack" => CompType.ModPack,
                    "resourcepack" => CompType.ResourcePack,
                    "shader" => CompType.Shader,
                    _ => CompType.Mod
                }
            };

            if (string.IsNullOrEmpty(result.LogoUrl))
                result.LogoUrl = null;

            result.Drops =
            [
                .. ((data["game_versions"] ?? data["versions"]) as JsonArray ?? [])
                .Select(v => McInstanceInfo.VersionToDrop((string?)v ?? ""))
                .Where(v => v > 0)
                .Distinct()
                .OrderByDescending(v => v)
            ];

            foreach (var category in (data["loaders"] as JsonArray)
                     ?.Select(t => t?.ToString())
                     ?? [])
                if (ModrinthLoaderTypes.TryGetValue(category ?? "", out var loader))
                    result.ModLoaders.Add(loader);

            foreach (var category in (data["categories"] as JsonArray)
                     ?.Select(t => t?.ToString())
                     ?? [])
            {
                if (string.IsNullOrEmpty(category)) continue;
                if (ModrinthLoaderTypes.TryGetValue(category, out var loader))
                {
                    result.ModLoaders.Add(loader);
                }
                else
                {
                    if (category.Equals("datapack", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Type = CompType.DataPack;
                    }
                    else
                    {
                        if (ResourcePackHiddenCategoryLangKeys.TryGetValue(
                                category,
                                out var hiddenLangKey))
                        {
                            if (result.Type != CompType.ResourcePack)
                                _AddTag(result.Tags, hiddenLangKey);
                        }
                        else
                        {
                            if (ModrinthCategoryLangKeys.TryGetValue(category, out var langKey))
                                _AddTag(result.Tags, langKey);
                        }
                    }
                }
            }

            return result;
        }

        private static CompType _GetCurseForgeTypeByWebsite(string website)
        {
            var websiteLower = (website ?? "").ToLowerInvariant();

            if (websiteLower.Contains("/mc-mods/") || websiteLower.Contains("/mod/"))
                return CompType.Mod;
            if (websiteLower.Contains("/modpacks/"))
                return CompType.ModPack;
            if (websiteLower.Contains("/resourcepacks/")
                || websiteLower.Contains("/texture-packs/"))
                return CompType.ResourcePack;
            if (websiteLower.Contains("/shaders/"))
                return CompType.Shader;
            if (websiteLower.Contains("/worlds/"))
                return CompType.World;

            return CompType.DataPack;
        }

        private static void _AddTag(List<string> tags, string langKey)
        {
            if (string.IsNullOrWhiteSpace(langKey))
                return;

            if (!tags.Contains(langKey))
                tags.Add(langKey);
        }

        private sealed class CompProjectBuildResult
        {
            public List<int> CurseForgeFileIds = [];
            public string Description = "";
            public int DownloadCount;
            public List<int> Drops = [];
            public bool FromCurseForge;
            public string Id = "";
            public DateTime? LastUpdate;
            public string? LogoUrl;
            public List<CompLoaderType> ModLoaders = [];
            public string RawName = "";
            public string Slug = "";
            public List<string> Tags = [];
            public CompType Type;
            public string Website = "";
        }
    }
}