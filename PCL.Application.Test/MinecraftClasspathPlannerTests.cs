// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Launch.Libraries;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftClasspathPlannerTests
{
    [TestMethod]
    public void CreatePlan_ShouldSkipNativeLibraries()
    {
        MinecraftClasspathPlan plan = MinecraftClasspathPlanner.CreatePlan(
            new MinecraftClasspathPlanRequest
            {
                Libraries =
                [
                    Token("native.jar") with { IsNatives = true },
                    Token("client.jar")
                ]
            });

        CollectionAssert.AreEqual(new[] { "client.jar" }, plan.Entries.ToArray());
    }

    [TestMethod]
    public void CreatePlan_ShouldSkipCleanroomConflictingLibraries()
    {
        MinecraftClasspathPlan plan = MinecraftClasspathPlanner.CreatePlan(
            new MinecraftClasspathPlanRequest
            {
                HasCleanroom = true,
                Libraries =
                [
                    Token("lwjgl.jar") with { OriginalName = "org.lwjgl.lwjgl:lwjgl:2.9.4" },
                    Token("jna.jar") with { OriginalName = "net.java.dev.jna:platform:3.4.0" },
                    Token("icu.jar") with { OriginalName = "com.ibm.icu:icu4j-core-mojang:51.2" },
                    Token("client.jar")
                ]
            });

        CollectionAssert.AreEqual(new[] { "client.jar" }, plan.Entries.ToArray());
    }

    [TestMethod]
    public void CreatePlan_ShouldPlaceOptiFineBeforeLastTwoEntries()
    {
        MinecraftClasspathPlan plan = MinecraftClasspathPlanner.CreatePlan(
            new MinecraftClasspathPlanRequest
            {
                Libraries =
                [
                    Token("a.jar"),
                    Token("optifine.jar") with
                    {
                        OriginalName = "optifine:OptiFine:HD_U_I7",
                        NameWithoutVersion = "optifine:OptiFine"
                    },
                    Token("b.jar"),
                    Token("client.jar")
                ]
            });

        CollectionAssert.AreEqual(new[] { "a.jar", "optifine.jar", "b.jar", "client.jar" }, plan.Entries.ToArray());
    }

    [TestMethod]
    public void CreatePlan_ShouldPutCustomClasspathHeadAtFrontInLegacyOrder()
    {
        MinecraftClasspathPlan plan = MinecraftClasspathPlanner.CreatePlan(
            new MinecraftClasspathPlanRequest
            {
                BundledClasspathEntries = ["retro-wrapper.jar"],
                ClasspathHeadEntries = ["first.jar", "second.jar", "", "  "],
                Libraries = [Token("client.jar")]
            });

        CollectionAssert.AreEqual(
            new[] { "second.jar", "first.jar", "retro-wrapper.jar", "client.jar" },
            plan.Entries.ToArray());
    }

    private static MinecraftLibraryToken Token(string localPath) => new()
    {
        OriginalName = "org.example:library:1.0",
        NameWithoutVersion = "org.example:library",
        LocalPath = localPath
    };
}
