// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Launch.Libraries;

public static class MinecraftClasspathRuleRegistry
{
    private static readonly MinecraftLibraryNameFragment[] CleanroomExclusions =
    [
        new("org.lwjgl.lwjgl:lwjgl:2.9.4"),
        new("net.java.dev.jna:platform:3.4.0"),
        new("com.ibm.icu:icu4j-core-mojang:51.2")
    ];

    public static ReadOnlySpan<MinecraftLibraryNameFragment> CleanroomExcludedLibraryFragments =>
        CleanroomExclusions;
}
