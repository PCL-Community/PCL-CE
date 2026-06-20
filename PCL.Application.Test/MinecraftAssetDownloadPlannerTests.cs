// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Assets;
using PCL.Application.Minecraft.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftAssetDownloadPlannerTests
{
    [TestMethod]
    public void CreatePlan_ShouldIncludeAllAssetsWhenHashCheckIsRequested()
    {
        MinecraftAssetDownloadPlan plan = MinecraftAssetDownloadPlanner.CreatePlan(
            new MinecraftAssetDownloadPlanRequest
            {
                CheckHash = true,
                Assets = [Asset("sound.ogg", "abcdef1234", 100)],
                ExistingFiles = new Dictionary<string, MinecraftAssetFileState>
                {
                    ["sound.ogg"] = new(Exists: true, Length: 100)
                }
            });

        Assert.AreEqual(1, plan.Files.Count);
        Assert.AreEqual("https://resources.download.minecraft.net/ab/abcdef1234", plan.Files[0].Url);
    }

    [TestMethod]
    public void CreatePlan_ShouldSkipExistingAssetWithMatchingSize()
    {
        MinecraftAssetDownloadPlan plan = MinecraftAssetDownloadPlanner.CreatePlan(
            new MinecraftAssetDownloadPlanRequest
            {
                Assets = [Asset("sound.ogg", "abcdef1234", 100)],
                ExistingFiles = new Dictionary<string, MinecraftAssetFileState>
                {
                    ["sound.ogg"] = new(Exists: true, Length: 100)
                }
            });

        Assert.AreEqual(0, plan.Files.Count);
    }

    [TestMethod]
    public void CreatePlan_ShouldSkipZeroSizedAssetWhenFileExists()
    {
        MinecraftAssetDownloadPlan plan = MinecraftAssetDownloadPlanner.CreatePlan(
            new MinecraftAssetDownloadPlanRequest
            {
                Assets = [Asset("legacy.dat", "bcdef12345", 0)],
                ExistingFiles = new Dictionary<string, MinecraftAssetFileState>
                {
                    ["legacy.dat"] = new(Exists: true, Length: 123)
                }
            });

        Assert.AreEqual(0, plan.Files.Count);
    }

    [TestMethod]
    public void CreatePlan_ShouldIncludeMissingOrSizeMismatchedAssets()
    {
        MinecraftAssetDownloadPlan plan = MinecraftAssetDownloadPlanner.CreatePlan(
            new MinecraftAssetDownloadPlanRequest
            {
                Assets =
                [
                    Asset("missing.ogg", "abcdef1234", 100),
                    Asset("wrong-size.ogg", "bcdef12345", 200),
                    Asset("ok.ogg", "cdef123456", 300)
                ],
                ExistingFiles = new Dictionary<string, MinecraftAssetFileState>
                {
                    ["wrong-size.ogg"] = new(Exists: true, Length: 199),
                    ["ok.ogg"] = new(Exists: true, Length: 300)
                }
            });

        CollectionAssert.AreEqual(
            new[] { "missing.ogg", "wrong-size.ogg" },
            plan.Files.Select(static file => file.LocalPath).ToArray());
        Assert.AreEqual(100, plan.Files[0].ActualSize);
        Assert.AreEqual(200, plan.Files[1].ActualSize);
    }

    [TestMethod]
    public void CreatePlan_ShouldUseUnknownActualSizeForZeroSizedAsset()
    {
        MinecraftAssetDownloadPlan plan = MinecraftAssetDownloadPlanner.CreatePlan(
            new MinecraftAssetDownloadPlanRequest
            {
                CheckHash = true,
                Assets = [Asset("legacy.dat", "bcdef12345", 0)]
            });

        Assert.AreEqual(-1, plan.Files[0].ActualSize);
    }

    private static MinecraftAssetToken Asset(string localPath, string hash, long size) => new()
    {
        LocalPath = localPath,
        SourcePath = localPath,
        Hash = hash,
        Size = size
    };
}
