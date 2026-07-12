// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using PCL.Application.Instances;
using PCL.Desktop.Features.Launching.Views;

namespace PCL.Desktop.Features.Instances.Views;

internal static class InstanceDisplayHelper
{
    public const string ImageAssetRoot = "avares://PCL.Desktop/Assets/Legacy/";
    public const string BlockAssetRoot = "avares://PCL.Desktop/Assets/Legacy/Blocks/";
    public const string DefaultLogo = BlockAssetRoot + "Grass.png";
    public const string ErrorLogo = BlockAssetRoot + "RedstoneBlock.png";
    public const string CustomLogoRelativePath = "PCL\\Logo.png";
    private const string WpfImagePrefix = "pack://application:,,,/images/";
    private static readonly string WpfPclImagePrefix =
        "pack://application:,,,/Plain Craft Launcher " + "2;component/Images/";

    public static string ResolveLogo(LaunchInstanceInfo instance, InstanceMetadata metadata)
    {
        string autoLogo = ResolveAutoLogo(instance);
        string logo = metadata.LogoPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(logo))
            return autoLogo;

        if (IsCustomLogoPath(logo))
        {
            string customLogo = GetCustomLogoPath(instance);
            return File.Exists(customLogo) ? customLogo : autoLogo;
        }

        if (Path.IsPathRooted(logo))
            return File.Exists(logo) ? logo : autoLogo;

        string normalizedLogo = NormalizeLogoPath(logo);
        if (normalizedLogo.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return normalizedLogo;

        if (normalizedLogo.Contains('/', StringComparison.Ordinal))
            return ImageAssetRoot + normalizedLogo;

        return BlockAssetRoot + normalizedLogo;
    }

    private static string ResolveAutoLogo(LaunchInstanceInfo instance) =>
        InferLogoFromVersionJson(instance.VersionJsonPath) ?? DefaultLogo;

    private static string? InferLogoFromVersionJson(string versionJsonPath)
    {
        if (!File.Exists(versionJsonPath))
            return null;

        try
        {
            using FileStream stream = File.OpenRead(versionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            List<string> signalParts =
            [
                ReadString(root, "id"),
                ReadString(root, "inheritsFrom"),
                ReadString(root, "type"),
                ReadString(root, "mainClass"),
                root.GetRawText()
            ];
            signalParts.AddRange(ReadLibraryNames(root));
            string signal = string.Join('\n', signalParts);

            return ResolveLogoFromVersionJson(root, signal);
        }
        catch (JsonException)
        {
            return ErrorLogo;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ResolveLogoFromVersionJson(JsonElement root, string signal)
    {
        string id = ReadString(root, "id");
        string inherited = ReadString(root, "inheritsFrom");
        string type = ReadString(root, "type");
        string logo = ResolveVanillaLogo(id, inherited, type);
        string lowerSignal = signal.ToLowerInvariant();

        if (lowerSignal.Contains("optifine", StringComparison.Ordinal))
            logo = BlockAssetRoot + "GrassPath.png";
        if (lowerSignal.Contains("liteloader", StringComparison.Ordinal))
            logo = BlockAssetRoot + "Egg.png";

        if (lowerSignal.Contains("labymod_data", StringComparison.Ordinal) ||
            lowerSignal.Contains("labymod", StringComparison.Ordinal))
        {
            return BlockAssetRoot + "LabyMod.png";
        }

        if (lowerSignal.Contains("net.legacyfabric:intermediary", StringComparison.Ordinal) ||
            lowerSignal.Contains("legacyfabric", StringComparison.Ordinal) ||
            lowerSignal.Contains("legacy-fabric", StringComparison.Ordinal))
        {
            return BlockAssetRoot + "Fabric.png";
        }

        if (lowerSignal.Contains("net.fabricmc:fabric-loader", StringComparison.Ordinal) ||
            lowerSignal.Contains("fabric-loader", StringComparison.Ordinal))
        {
            return BlockAssetRoot + "Fabric.png";
        }

        if (lowerSignal.Contains("org.quiltmc:quilt-loader", StringComparison.Ordinal) ||
            lowerSignal.Contains("quilt-loader", StringComparison.Ordinal))
        {
            return BlockAssetRoot + "Quilt.png";
        }

        if (lowerSignal.Contains("com.cleanroommc:cleanroom:", StringComparison.Ordinal) ||
            lowerSignal.Contains("cleanroom", StringComparison.Ordinal))
        {
            return BlockAssetRoot + "Cleanroom.png";
        }

        // NeoForge before Forge — "forge" is a substring of "neoforge"/"neoforged".
        if (lowerSignal.Contains("net.neoforged", StringComparison.Ordinal) ||
            lowerSignal.Contains("net.neoforge", StringComparison.Ordinal) ||
            lowerSignal.Contains("neoforge", StringComparison.Ordinal))
        {
            return BlockAssetRoot + "NeoForge.png";
        }

        if (lowerSignal.Contains("net.minecraftforge:forge", StringComparison.Ordinal) ||
            (lowerSignal.Contains("minecraftforge", StringComparison.Ordinal) &&
             !lowerSignal.Contains("neoforge", StringComparison.Ordinal)))
        {
            return BlockAssetRoot + "Anvil.png";
        }

        return logo;
    }

    private static string ResolveVanillaLogo(string id, string inherited, string type)
    {
        if (IsAprilFoolsVersion(id) || IsAprilFoolsVersion(inherited) ||
            type.Equals("fool", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("special", StringComparison.OrdinalIgnoreCase))
        {
            return BlockAssetRoot + "GoldBlock.png";
        }

        if (type.Equals("old_beta", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("old_alpha", StringComparison.OrdinalIgnoreCase))
        {
            return BlockAssetRoot + "CobbleStone.png";
        }

        if (IsSnapshotVersion(id, inherited, type))
            return BlockAssetRoot + "CommandBlock.png";

        return DefaultLogo;
    }

    private static bool IsSnapshotVersion(string id, string inherited, string type) =>
        type.Equals("snapshot", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("pending", StringComparison.OrdinalIgnoreCase) ||
        IsSnapshotName(id) ||
        IsSnapshotName(inherited);

    private static bool IsSnapshotName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Contains('w', StringComparison.OrdinalIgnoreCase) ||
               value.Contains("snapshot", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("rc", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("pre", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("experimental", StringComparison.OrdinalIgnoreCase) ||
               value.Contains('-', StringComparison.Ordinal) ||
               value.Contains("combat", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAprilFoolsVersion(string id)
    {
        string value = id.Trim().ToLowerInvariant();
        return value.StartsWith("2.0", StringComparison.Ordinal) ||
               value.StartsWith("2point0", StringComparison.Ordinal) ||
               value is "20w14infinite" or "20w14∞" or "3d shareware v1.34" or "1.rv-pre1" or
                   "15w14a" or "22w13oneblockatatime" or "23w13a_or_b" or "24w14potato" or
                   "25w14craftmine" or "26w14a";
    }

    private static string NormalizeLogoPath(string logo)
    {
        string trimmed = logo.Trim();
        if (trimmed.StartsWith(WpfImagePrefix, StringComparison.OrdinalIgnoreCase))
            return ImageAssetRoot + trimmed[WpfImagePrefix.Length..].Replace('\\', '/');

        if (trimmed.StartsWith(WpfPclImagePrefix, StringComparison.OrdinalIgnoreCase))
            return ImageAssetRoot + trimmed[WpfPclImagePrefix.Length..].Replace('\\', '/');

        if (trimmed.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            return trimmed.Replace('\\', '/');

        if (trimmed.StartsWith("/Images/", StringComparison.OrdinalIgnoreCase))
            return trimmed["/Images/".Length..].Replace('\\', '/');

        if (trimmed.StartsWith("Images/", StringComparison.OrdinalIgnoreCase))
            return trimmed["Images/".Length..].Replace('\\', '/');

        return trimmed.Replace('\\', '/');
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static IEnumerable<string> ReadLibraryNames(JsonElement root)
    {
        if (!root.TryGetProperty("libraries", out JsonElement libraries) ||
            libraries.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (JsonElement library in libraries.EnumerateArray())
        {
            string name = ReadString(library, "name");
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }
    }

    public static bool IsModable(LaunchInstanceInfo instance)
    {
        if (!File.Exists(instance.VersionJsonPath))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            return ReadLibraryNames(document.RootElement).Any(IsModLoaderLibrary);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Shader packs UI: show only when OptiFine / Iris / Oculus / Sodium+Iris-class mods exist.
    /// </summary>
    public static bool HasShaderSupport(LaunchInstanceInfo instance)
    {
        if (HasLibraryNeedle(instance, "optifine", "optifine:optifine"))
            return true;

        return HasModJarNeedle(
            instance,
            "iris",
            "oculus",
            "optifine",
            "optifabric",
            "canvas-renderer",
            "angelica");
    }

    /// <summary>
    /// Schematics / 投影 UI: show only when a projection mod is installed (Litematica, etc.).
    /// </summary>
    public static bool HasSchematicSupport(LaunchInstanceInfo instance) =>
        HasModJarNeedle(
            instance,
            "litematica",
            "schematica",
            "worldedit",
            "axiom",
            "syncmatica",
            "baritone");

    private static bool HasLibraryNeedle(LaunchInstanceInfo instance, params string[] needles)
    {
        if (!File.Exists(instance.VersionJsonPath))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            return ReadLibraryNames(document.RootElement)
                .Any(library => needles.Any(n => library.Contains(n, StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasModJarNeedle(LaunchInstanceInfo instance, params string[] needles)
    {
        foreach (string modsDir in EnumerateModsDirectories(instance))
        {
            try
            {
                if (!Directory.Exists(modsDir))
                    continue;
                foreach (string file in Directory.EnumerateFiles(modsDir, "*.jar*", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    if (needles.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateModsDirectories(LaunchInstanceInfo instance)
    {
        yield return Path.Combine(instance.InstanceDirectory, "mods");
        DirectoryInfo versionDirectory = new(instance.InstanceDirectory);
        if (versionDirectory.Parent?.Parent is { } minecraftRoot)
            yield return Path.Combine(minecraftRoot.FullName, "mods");
    }

    public static bool IsValid(LaunchInstanceInfo instance)
    {
        if (!File.Exists(instance.VersionJsonPath))
            return false;

        try
        {
            using FileStream stream = File.OpenRead(instance.VersionJsonPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsModLoaderLibrary(string library) =>
        library.Contains("net.minecraftforge:forge:", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("minecraftforge", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("net.neoforged:neoforge:", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("net.neoforge:forge:", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("neoforge", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("com.cleanroommc:cleanroom:", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("cleanroom", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("net.fabricmc:fabric-loader:", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("org.quiltmc:quilt-loader:", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("optifine", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("liteloader", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("net.legacyfabric:", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("legacyfabric", StringComparison.OrdinalIgnoreCase) ||
        library.Contains("labymod", StringComparison.OrdinalIgnoreCase);

    public static string GetCustomLogoPath(LaunchInstanceInfo instance) =>
        Path.Combine(instance.InstanceDirectory, "PCL", "Logo.png");

    public static bool IsCustomLogoPath(string? logo) =>
        !string.IsNullOrWhiteSpace(logo) &&
        logo.Replace('/', '\\').EndsWith(CustomLogoRelativePath, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeLogoTag(string? logo) =>
        string.IsNullOrWhiteSpace(logo) ? string.Empty : NormalizeLogoPath(logo);

    public static string GetCardTitle(int cardType, bool isStarred, int count)
    {
        if (isStarred)
            return "收藏夹";

        return Math.Clamp(cardType, 0, 5) switch
        {
            1 => $"隐藏版本 ({count})",
            2 => $"可安装 Mod ({count})",
            4 => $"不常用版本 ({count})",
            5 => $"愚人节版本 ({count})",
            _ => $"常规版本 ({count})"
        };
    }
}
