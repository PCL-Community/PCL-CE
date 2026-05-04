namespace PCL.Core.Minecraft.Download;

/// <summary>
///     Central registry defining all official download source URLs.
///     These are the canonical "where to get data" definitions.
/// </summary>
public static class DownloadRegistry
{
    // --- Minecraft Official Sources ---
    public const string LauncherMeta = "https://launchermeta.mojang.com";
    public const string PistonData = "https://piston-data.mojang.com";
    public const string PistonMeta = "https://piston-meta.mojang.com";
    public const string ResourcesLegacy = "http://resources.download.minecraft.net";
    public const string Resources = "https://resources.download.minecraft.net";
    public const string Libraries = "https://libraries.minecraft.net";
    public const string Launcher = "https://launcher.mojang.com";

    // --- Minecraft Version List ---
    public const string VersionManifest = "https://launchermeta.mojang.com/mc/game/version_manifest.json";

    // --- Unlisted Version Meta ---
    public const string UnlistedVersionsJson = "https://zkitefly.github.io/unlisted-versions-of-minecraft/version_manifest.json";

    // --- Fabric ---
    public const string FabricMeta = "https://meta.fabricmc.net/v2/versions";

    // --- Forge ---
    public const string ForgeKnownVersions = "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html";

    public static string ForgeVersionList(string mcVersion)
    {
        return $"https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_{mcVersion}.html";
    }

    // --- NeoForge ---
    public const string NeoForgeVersionsLatest = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";
    public const string NeoForgeVersionsLegacy = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge";

    public static string NeoForgeDownloadUrl(string packageName, string apiName)
    {
        return $"https://maven.neoforged.net/releases/net/neoforged/{packageName}/{apiName}/{packageName}-{apiName}";
    }

    // --- OptiFine ---
    public const string OptiFineList = "https://optifine.net/downloads";

    // --- LiteLoader ---
    public const string LiteLoaderVersions = "https://dl.liteloader.com/versions/versions.json";

    // --- Quilt ---
    public const string QuiltMeta = "https://meta.quiltmc.org/v3/versions";

    // --- LegacyFabric ---
    public const string LegacyFabricMeta = "https://meta.legacyfabric.net/v2/versions";

    // --- LabyMod ---
    public const string LabyModProduction = "https://releases.r2.labymod.net/api/v1/manifest/production/latest.json";
    public const string LabyModSnapshot = "https://releases.r2.labymod.net/api/v1/manifest/snapshot/latest.json";

    // --- Cleanroom ---
    public const string CleanroomReleases = "https://api.github.com/repos/CleanroomMC/Cleanroom/releases";

    public static string CleanroomDownloadUrl(string apiName)
    {
        return $"https://github.com/CleanroomMC/Cleanroom/releases/download/{apiName}/cleanroom-{apiName}";
    }

    // --- Mod Platforms ---
    public const string ModrinthApi = "https://api.modrinth.com";
    public const string CurseForgeApi = "https://api.curseforge.com";
    public const string ModrinthCdn = "https://cdn.modrinth.com";
    public const string CurseForgeCdn = "https://edge.forgecdn.net";
}
