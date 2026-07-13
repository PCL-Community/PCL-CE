// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Desktop.Features.Launching.Views;
using PCL.Desktop.Features.Shared;

namespace PCL.Desktop.Features.Community;

public static class CommunityInstanceCompatibility
{
    public static CommunitySearchOptions Apply(
        CommunitySearchOptions options,
        CommunityResourceCategory category,
        LaunchInstanceInfo instance)
    {
        MinecraftVersionJsonInfo versionInfo = MinecraftVersionJsonInspector.Read(instance);
        string? loader = options.Loader;
        if (string.IsNullOrWhiteSpace(loader) &&
            category is CommunityResourceCategory.Mod or CommunityResourceCategory.Modpack)
        {
            loader = DetectLoader(versionInfo.Libraries);
        }

        return options with
        {
            GameVersion = string.IsNullOrWhiteSpace(options.GameVersion)
                ? versionInfo.MinecraftVersionId
                : options.GameVersion,
            Loader = loader
        };
    }

    private static string? DetectLoader(IReadOnlyList<string> libraries)
    {
        if (libraries.Any(static library =>
                library.Contains("net.legacyfabric:", StringComparison.OrdinalIgnoreCase)))
        {
            return "legacy-fabric";
        }

        if (libraries.Any(static library =>
                library.Contains("net.fabricmc:fabric-loader:", StringComparison.OrdinalIgnoreCase)))
        {
            return "fabric";
        }

        if (libraries.Any(static library =>
                library.Contains("org.quiltmc:quilt-loader:", StringComparison.OrdinalIgnoreCase)))
        {
            return "quilt";
        }

        if (libraries.Any(static library =>
                library.Contains("net.neoforged:", StringComparison.OrdinalIgnoreCase)))
        {
            return "neoforge";
        }

        if (libraries.Any(static library =>
                library.Contains("net.minecraftforge:forge:", StringComparison.OrdinalIgnoreCase)))
        {
            return "forge";
        }

        return libraries.Any(static library =>
            library.Contains("liteloader", StringComparison.OrdinalIgnoreCase))
            ? "liteloader"
            : null;
    }
}
