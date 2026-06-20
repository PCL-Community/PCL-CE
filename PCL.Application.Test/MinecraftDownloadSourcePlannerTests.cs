// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftDownloadSourcePlannerTests
{
    [TestMethod]
    public void GetAssetSources_ShouldPreferMirrorWhenRequested()
    {
        string[] sources = MinecraftDownloadSourcePlanner.GetAssetSources(
            "http://resources.download.minecraft.net/ab/abcdef",
            preferOfficialSource: false);

        CollectionAssert.AreEqual(
            new[]
            {
                "https://bmclapi2.bangbang93.com/assets/ab/abcdef",
                "https://resources.download.minecraft.net/ab/abcdef"
            },
            sources);
    }

    [TestMethod]
    public void GetLibrarySources_ShouldExcludeOfficialForThirdPartyMaven()
    {
        string[] sources = MinecraftDownloadSourcePlanner.GetLibrarySources(
            "https://maven.minecraftforge.net/net/minecraftforge/forge/1.20.1/forge.jar",
            preferOfficialSource: true);

        Assert.AreEqual(2, sources.Length);
        Assert.IsTrue(sources.All(static source => source.StartsWith("https://bmclapi2.bangbang93.com/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void GetLauncherOrMetaSources_ShouldPreferOfficialWhenRequested()
    {
        string[] sources = MinecraftDownloadSourcePlanner.GetLauncherOrMetaSources(
            "https://launcher.mojang.com/v1/objects/client.jar",
            preferOfficialSource: true);

        CollectionAssert.AreEqual(
            new[]
            {
                "https://launcher.mojang.com/v1/objects/client.jar",
                "https://bmclapi2.bangbang93.com/v1/objects/client.jar"
            },
            sources);
    }
}
