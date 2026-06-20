// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Downloads;

public static class MinecraftDownloadSourcePlanner
{
    public static string[] OrderSources(
        IEnumerable<string> officialUrls,
        IEnumerable<string> mirrorUrls,
        bool preferOfficialSource) =>
        (preferOfficialSource ? officialUrls.Union(mirrorUrls) : mirrorUrls.Union(officialUrls)).ToArray();

    public static string[] GetAssetSources(string original, bool preferOfficialSource)
    {
        ArgumentNullException.ThrowIfNull(original);
        original = original.Replace(
            "http://resources.download.minecraft.net",
            "https://resources.download.minecraft.net",
            StringComparison.Ordinal);

        return OrderSources([original], [ReplaceAssetMirrorHost(original)], preferOfficialSource);
    }

    public static string[] GetLibrarySources(string original, bool preferOfficialSource)
    {
        ArgumentNullException.ThrowIfNull(original);
        string[] mirrorUrls =
        [
            ReplaceLibraryMirrorHost(original, "https://bmclapi2.bangbang93.com/maven"),
            ReplaceLibraryMirrorHost(original, "https://bmclapi2.bangbang93.com/libraries"),
            original
        ];

        if (ContainsThirdPartyMaven(original))
            return mirrorUrls.Take(2).ToArray();

        return OrderSources([original], mirrorUrls, preferOfficialSource);
    }

    public static string[] GetLauncherOrMetaSources(string original, bool preferOfficialSource)
    {
        ArgumentNullException.ThrowIfNull(original);
        string mirrorUrl = original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.Ordinal)
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.Ordinal)
            .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.Ordinal)
            .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.Ordinal)
            .Replace(
                "https://zkitefly.github.io/unlisted-versions-of-minecraft",
                "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto",
                StringComparison.Ordinal);

        return OrderSources([original], [mirrorUrl, original], preferOfficialSource);
    }

    private static string ReplaceAssetMirrorHost(string original) =>
        original
            .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com/assets", StringComparison.Ordinal)
            .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com/assets", StringComparison.Ordinal)
            .Replace(
                "https://resources.download.minecraft.net",
                "https://bmclapi2.bangbang93.com/assets",
                StringComparison.Ordinal);

    private static string ReplaceLibraryMirrorHost(string original, string mirrorHost) =>
        original
            .Replace("https://piston-data.mojang.com", mirrorHost, StringComparison.Ordinal)
            .Replace("https://piston-meta.mojang.com", mirrorHost, StringComparison.Ordinal)
            .Replace("https://libraries.minecraft.net", mirrorHost, StringComparison.Ordinal)
            .Replace("https://maven.minecraftforge.net", mirrorHost, StringComparison.Ordinal)
            .Replace("https://maven.fabricmc.net", mirrorHost, StringComparison.Ordinal)
            .Replace("https://maven.neoforged.net/releases", mirrorHost, StringComparison.Ordinal)
            .Replace(
                "https://zkitefly.github.io/unlisted-versions-of-minecraft",
                "https://alist.8mi.tech/d/mirror/unlisted-versions-of-minecraft/Auto",
                StringComparison.Ordinal);

    private static bool ContainsThirdPartyMaven(string original) =>
        original.Contains("minecraftforge", StringComparison.Ordinal) ||
        original.Contains("fabricmc", StringComparison.Ordinal) ||
        original.Contains("neoforged", StringComparison.Ordinal);
}
