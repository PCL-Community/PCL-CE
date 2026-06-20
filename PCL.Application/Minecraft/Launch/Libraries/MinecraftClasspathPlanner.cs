// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Libraries;

public static class MinecraftClasspathPlanner
{
    private static readonly string[] CleanroomExcludedLibraryFragments =
    [
        "org.lwjgl.lwjgl:lwjgl:2.9.4",
        "net.java.dev.jna:platform:3.4.0",
        "com.ibm.icu:icu4j-core-mojang:51.2"
    ];

    public static MinecraftClasspathPlan CreatePlan(MinecraftClasspathPlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Libraries);

        List<string> entries = [];
        foreach (string entry in request.BundledClasspathEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry))
                entries.Add(entry);
        }

        string? optiFinePath = null;
        foreach (MinecraftLibraryToken library in request.Libraries)
        {
            if (library.IsNatives || string.IsNullOrWhiteSpace(library.LocalPath))
                continue;
            if (request.HasCleanroom && ShouldSkipForCleanroom(library.OriginalName))
                continue;
            if (string.Equals(library.NameWithoutVersion, "optifine:OptiFine", StringComparison.Ordinal))
            {
                optiFinePath = library.LocalPath;
                continue;
            }

            entries.Add(library.LocalPath);
        }

        foreach (string entry in request.ClasspathHeadEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry))
                entries.Insert(0, entry);
        }

        if (!string.IsNullOrWhiteSpace(optiFinePath))
            entries.Insert(Math.Max(0, entries.Count - 2), optiFinePath);

        return new MinecraftClasspathPlan(entries);
    }

    private static bool ShouldSkipForCleanroom(string? originalName)
    {
        if (originalName is null)
            return false;

        foreach (string fragment in CleanroomExcludedLibraryFragments)
        {
            if (originalName.Contains(fragment, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
