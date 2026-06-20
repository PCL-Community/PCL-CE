// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Launch.Libraries;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftLibraryDownloadPlannerTests
{
    [TestMethod]
    public void CreatePlan_ShouldOrderLibrarySourcesByPreference()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-download-plan");
        string localPath = Path.Combine(root, "libraries", "org", "lwjgl", "lwjgl", "3.3.3", "lwjgl-3.3.3.jar");

        MinecraftLibraryDownloadFile file = MinecraftLibraryDownloadPlanner.CreatePlan(
            new MinecraftLibraryDownloadPlanRequest
            {
                MinecraftRootDirectory = root,
                PreferOfficialSource = false,
                Libraries =
                [
                    Token(localPath) with
                    {
                        Url = "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar"
                    }
                ]
            }).DownloadFiles[0];

        Assert.AreEqual("https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar", file.Urls[0]);
        CollectionAssert.Contains(file.Urls.ToList(), "https://bmclapi2.bangbang93.com/maven/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar");
        CollectionAssert.Contains(file.Urls.ToList(), "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar");
    }

    [TestMethod]
    public void CreatePlan_ShouldUseOnlyMirrorsForThirdPartyMavenFallback()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-download-plan");
        string localPath = Path.Combine(root, "libraries", "net", "minecraftforge", "forge", "1.20.1", "forge-1.20.1.jar");

        MinecraftLibraryDownloadFile file = MinecraftLibraryDownloadPlanner.CreatePlan(
            new MinecraftLibraryDownloadPlanRequest
            {
                MinecraftRootDirectory = root,
                Libraries = [Token(localPath)]
            }).DownloadFiles[0];

        Assert.AreEqual(2, file.Urls.Count);
        Assert.IsTrue(file.Urls.All(static url => url.StartsWith("https://bmclapi2.bangbang93.com/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void CreatePlan_ShouldSkipLocalLibraries()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-download-plan");

        MinecraftLibraryDownloadPlan plan = MinecraftLibraryDownloadPlanner.CreatePlan(
            new MinecraftLibraryDownloadPlanRequest
            {
                MinecraftRootDirectory = root,
                Libraries =
                [
                    Token(Path.Combine(root, "local.jar")) with
                    {
                        OriginalName = "local:lib:1.0",
                        IsLocal = true
                    }
                ]
            });

        Assert.AreEqual(0, plan.DownloadFiles.Count);
        CollectionAssert.Contains(plan.SkippedLocalLibraries.ToList(), "local:lib:1.0");
    }

    [TestMethod]
    public void CreatePlan_ShouldRequestBundledTransformerRelease()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-download-plan");
        string localPath = Path.Combine(root, "libraries", "cpw", "mods", "securejarhandler", "transformer-discovery-service.jar");

        MinecraftLibraryDownloadPlan plan = MinecraftLibraryDownloadPlanner.CreatePlan(
            new MinecraftLibraryDownloadPlanRequest
            {
                MinecraftRootDirectory = root,
                Libraries = [Token(localPath)]
            });

        Assert.AreEqual(0, plan.DownloadFiles.Count);
        Assert.AreEqual(1, plan.BundledFiles.Count);
        Assert.AreEqual("Resources/transformer.jar", plan.BundledFiles[0].ResourceName);
        Assert.AreEqual(localPath, plan.BundledFiles[0].LocalPath);
    }

    [TestMethod]
    public void CreatePlan_ShouldIgnoreLabyModSize()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-download-plan");
        string localPath = Path.Combine(root, "libraries", "net", "labymod", "api", "1.0", "api-1.0.jar");

        MinecraftLibraryDownloadFile file = MinecraftLibraryDownloadPlanner.CreatePlan(
            new MinecraftLibraryDownloadPlanRequest
            {
                MinecraftRootDirectory = root,
                Libraries =
                [
                    Token(localPath) with
                    {
                        OriginalName = "net.labymod:LabyMod:1.0",
                        NameWithoutVersion = "net.labymod:LabyMod",
                        Url = "https://example.test/labymod.jar",
                        Size = 999
                    }
                ]
            }).DownloadFiles[0];

        Assert.IsTrue(file.IgnoreSize);
        Assert.AreEqual(-1, file.ActualSize);
        Assert.AreEqual("https://example.test/labymod.jar", file.Urls[0]);
    }

    private static MinecraftLibraryToken Token(string localPath) => new()
    {
        OriginalName = "org.lwjgl:lwjgl:3.3.3",
        NameWithoutVersion = "org.lwjgl:lwjgl",
        LocalPath = localPath,
        Sha1 = "abcdef",
        Size = 123
    };
}
