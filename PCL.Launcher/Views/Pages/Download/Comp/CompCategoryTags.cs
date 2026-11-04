namespace PCL.Views.Pages.Download;

public sealed record CompCategoryTag(
    string TextKey,
    string? Tag,
    bool IsIndented = false)
{
    public bool IsGroupHeader => Tag is null;

    public static CompCategoryTag Header(string textKey)
    {
        return new CompCategoryTag(textKey, null);
    }

    public static CompCategoryTag Item(string textKey, string tag)
    {
        return new CompCategoryTag(textKey, tag, true);
    }
}

public static class CompCategoryTags
{
    public static readonly IReadOnlyList<CompCategoryTag> Mod =
    [
        new("Download.Comp.Category.WorldGen", "406/worldgen"),
        new("Download.Comp.Category.Biomes", "407/"),
        new("Download.Comp.Category.Dimensions", "410/"),
        new("Download.Comp.Category.OresResources", "408/"),
        new("Download.Comp.Category.Structures", "409/"),
        new("Download.Comp.Category.Technology", "412/technology"),
        new("Download.Comp.Category.PipesLogistics", "415/"),
        new("Download.Comp.Category.Automation", "4843/"),
        new("Download.Comp.Category.Energy", "417/"),
        new("Download.Comp.Category.Redstone", "4558/"),
        new("Download.Comp.Category.FoodCooking", "436/food"),
        new("Download.Comp.Category.Farming", "416/"),
        new("Download.Comp.Category.GameMechanics", "/game-mechanics"),
        new("Download.Comp.Category.Transportation", "414/transportation"),
        new("Download.Comp.Category.Storage", "420/storage"),
        new("Download.Comp.Category.Magic", "419/magic"),
        new("Download.Comp.Category.Adventure", "422/adventure"),
        new("Download.Comp.Category.Decoration", "424/decoration"),
        new("Download.Comp.Category.Mobs", "411/mobs"),
        new("Download.Comp.Category.Utility", "5191/utility"),
        new("Download.Comp.Category.Equipment", "434/equipment"),
        new("Download.Comp.Category.Creative", "9026/"),
        new("Download.Comp.Category.Optimization", "6814/optimization"),
        new("Download.Comp.Category.Display", "423/"),
        new("Download.Comp.Category.Server", "435/social"),
        new("Download.Comp.Category.Library", "421/library")
    ];

    public static readonly IReadOnlyList<CompCategoryTag> ModPack =
    [
        new("Download.Comp.Category.Multiplayer", "4484/"),
        new("Download.Comp.Category.Optimization", "/optimization"),
        new("Download.Comp.Category.Modpack.Hardcore", "4479/challenging"),
        new("Download.Comp.Category.Combat", "4483/combat"),
        new("Download.Comp.Category.Modpack.Quests", "4478/quests"),
        new("Download.Comp.Category.Technology", "4472/technology"),
        new("Download.Comp.Category.Magic", "4473/magic"),
        new("Download.Comp.Category.Adventure", "4475/adventure"),
        new("Download.Comp.Category.Modpack.KitchenSink", "/kitchen-sink"),
        new("Download.Comp.Category.Modpack.Exploration", "4476/"),
        new("Download.Comp.Category.Modpack.MiniGame", "4477/"),
        new("Download.Comp.Category.Modpack.SciFi", "4471/"),
        new("Download.Comp.Category.Modpack.Skyblock", "4736/"),
        new("Download.Comp.Category.Modpack.VanillaPlus", "5128/"),
        new("Download.Comp.Category.Modpack.Ftb", "4487/"),
        new("Download.Comp.Category.Modpack.MapBased", "4480/"),
        new("Download.Comp.Category.Modpack.SmallLight", "4481/lightweight"),
        new("Download.Comp.Category.Modpack.ExtraLarge", "4482/")
    ];

    public static readonly IReadOnlyList<CompCategoryTag> DataPack =
    [
        new("Download.Comp.Category.WorldGen", "/worldgen"),
        new("Download.Comp.Category.Technology", "6951/technology"),
        new("Download.Comp.Category.GameMechanics", "/game-mechanics"),
        new("Download.Comp.Category.Transportation", "/transportation"),
        new("Download.Comp.Category.Storage", "/storage"),
        new("Download.Comp.Category.Magic", "6952/magic"),
        new("Download.Comp.Category.Adventure", "6948/adventure"),
        new("Download.Comp.Category.DataPack.Fantasy", "6949/"),
        new("Download.Comp.Category.Decoration", "/decoration"),
        new("Download.Comp.Category.Mobs", "/mobs"),
        new("Download.Comp.Category.Utility", "6953/utility"),
        new("Download.Comp.Category.Equipment", "/equipment"),
        new("Download.Comp.Category.Optimization", "/optimization"),
        new("Download.Comp.Category.Server", "/social"),
        new("Download.Comp.Category.Library", "6950/library"),
        new("Download.Comp.Category.Mod.ModRelated", "6946/")
    ];

    public static readonly IReadOnlyList<CompCategoryTag> ResourcePack =
    [
        CompCategoryTag.Header("Download.Comp.Category.Style"),
        CompCategoryTag.Item("Download.Comp.Category.VanillaLike", "403/vanilla-like"),
        CompCategoryTag.Item("Download.Comp.Category.Realistic", "400/realistic"),
        CompCategoryTag.Item("Download.Comp.Category.Modern", "401/"),
        CompCategoryTag.Item("Download.Comp.Category.Medieval", "402/"),
        CompCategoryTag.Item("Download.Comp.Category.Steampunk", "399/"),
        CompCategoryTag.Item("Download.Comp.Category.Themed", "/themed"),
        CompCategoryTag.Item("Download.Comp.Category.Simplistic", "/simplistic"),
        CompCategoryTag.Item("Download.Comp.Category.Decoration", "/decoration"),
        CompCategoryTag.Item("Download.Comp.Category.Combat", "/combat"),
        CompCategoryTag.Item("Download.Comp.Category.Utility", "/utility"),
        CompCategoryTag.Item("Download.Comp.Category.Tweaks", "/tweaks"),
        CompCategoryTag.Item("Download.Comp.Category.Cursed", "/cursed"),

        CompCategoryTag.Header("Download.Comp.Category.Feature"),
        CompCategoryTag.Item("Download.Comp.Category.Entities", "/entities"),
        CompCategoryTag.Item("Download.Comp.Category.Audio", "/audio"),
        CompCategoryTag.Item("Download.Comp.Category.Fonts", "5244/fonts"),
        CompCategoryTag.Item("Download.Comp.Category.Models", "/models"),
        CompCategoryTag.Item("Download.Comp.Category.Locale", "/locale"),
        CompCategoryTag.Item("Download.Comp.Category.Gui", "/gui"),
        CompCategoryTag.Item("Download.Comp.Category.CoreShaders", "/core-shaders"),
        CompCategoryTag.Item("Download.Comp.Category.Animated", "404/"),
        CompCategoryTag.Item("Download.Comp.Category.ModSupport", "4465/modded"),

        CompCategoryTag.Header("Download.Comp.Category.Resolution"),
        CompCategoryTag.Item("Download.Comp.Category.ResourcePack.Resolution8xOrLower", "/8x-"),
        CompCategoryTag.Item("Download.Comp.Category.ResourcePack.Resolution16x", "393/16x"),
        CompCategoryTag.Item("Download.Comp.Category.ResourcePack.Resolution32x", "394/32x"),
        CompCategoryTag.Item("Download.Comp.Category.ResourcePack.Resolution48x", "/48x"),
        CompCategoryTag.Item("Download.Comp.Category.ResourcePack.Resolution64x", "395/64x"),
        CompCategoryTag.Item("Download.Comp.Category.ResourcePack.Resolution128x", "396/128x"),
        CompCategoryTag.Item("Download.Comp.Category.ResourcePack.Resolution256x", "397/256x"),
        CompCategoryTag.Item(
            "Download.Comp.Category.ResourcePack.Resolution512xOrHigher",
            "398/512x+")
    ];

    public static readonly IReadOnlyList<CompCategoryTag> Shader =
    [
        CompCategoryTag.Header("Download.Comp.Category.Style"),
        CompCategoryTag.Item("Download.Comp.Category.VanillaLike", "6555/vanilla-like"),
        CompCategoryTag.Item("Download.Comp.Category.Fantasy", "6554/fantasy"),
        CompCategoryTag.Item("Download.Comp.Category.Realistic", "6553/realistic"),
        CompCategoryTag.Item("Download.Comp.Category.SemiRealistic", "/semi-realistic"),
        CompCategoryTag.Item("Download.Comp.Category.Cartoon", "/cartoon"),

        CompCategoryTag.Header("Download.Comp.Category.Feature"),
        CompCategoryTag.Item("Download.Comp.Category.ColoredLighting", "/colored-lighting"),
        CompCategoryTag.Item("Download.Comp.Category.PathTracing", "/path-tracing"),
        CompCategoryTag.Item("Download.Comp.Category.Pbr", "/pbr"),
        CompCategoryTag.Item("Download.Comp.Category.Reflections", "/reflections"),

        CompCategoryTag.Header("Download.Comp.Category.Performance"),
        CompCategoryTag.Item("Download.Comp.Category.PerformancePotato", "/potato"),
        CompCategoryTag.Item("Download.Comp.Category.PerformanceLow", "/low"),
        CompCategoryTag.Item("Download.Comp.Category.PerformanceMedium", "/medium"),
        CompCategoryTag.Item("Download.Comp.Category.PerformanceHigh", "/high")
    ];

    public static readonly IReadOnlyList<CompCategoryTag> World =
    [
        new("Download.Comp.Category.World.Adventure", "248/"),
        new("Download.Comp.Category.World.Creative", "249/"),
        new("Download.Comp.Category.Modpack.MiniGame", "250/"),
        new("Download.Comp.Category.World.Parkour", "251/"),
        new("Download.Comp.Category.World.Puzzle", "252/"),
        new("Download.Comp.Category.World.Survival", "253/"),
        new("Download.Comp.Category.World.ModWorld", "4464/")
    ];

    public static IReadOnlyList<CompCategoryTag> For(Comp.CompType type)
    {
        return type switch
        {
            Comp.CompType.Mod => Mod,
            Comp.CompType.ModPack => ModPack,
            Comp.CompType.DataPack => DataPack,
            Comp.CompType.ResourcePack => ResourcePack,
            Comp.CompType.Shader => Shader,
            Comp.CompType.World => World,
            _ => []
        };
    }
}