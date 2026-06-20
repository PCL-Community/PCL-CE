// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Assets;
using PCL.Application.Minecraft.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftClientDownloadPlannerTests
{
    [TestMethod]
    public void CreateClientJarPlan_ShouldReadClientDownloadInfo()
    {
        string instanceDirectory = Path.Combine(Path.GetTempPath(), "pcl-client", "versions", "1.20.1");

        MinecraftClientJarDownloadPlan plan = MinecraftClientDownloadPlanner.CreateClientJarPlan(
            new MinecraftClientJarDownloadPlanRequest
            {
                InstanceDirectory = instanceDirectory,
                VersionName = "1.20.1",
                VersionJson = Json("""
                {
                  "downloads": {
                    "client": {
                      "url": "https://launcher.mojang.com/client.jar",
                      "size": 12345,
                      "sha1": "abcdef"
                    }
                  }
                }
                """)
            });

        Assert.AreEqual(MinecraftClientDownloadFailureReason.None, plan.FailureReason);
        Assert.IsNotNull(plan.File);
        Assert.AreEqual("https://launcher.mojang.com/client.jar", plan.File.Url);
        Assert.AreEqual(Path.Combine(instanceDirectory, "1.20.1.jar"), plan.File.LocalPath);
        Assert.AreEqual(1024L, plan.File.MinimumSize);
        Assert.AreEqual(12345L, plan.File.ActualSize);
        Assert.AreEqual("abcdef", plan.File.Sha1);
    }

    [TestMethod]
    public void CreateClientJarPlan_ShouldReportMissingDownloadInfo()
    {
        MinecraftClientJarDownloadPlan plan = MinecraftClientDownloadPlanner.CreateClientJarPlan(
            new MinecraftClientJarDownloadPlanRequest
            {
                InstanceDirectory = Path.Combine(Path.GetTempPath(), "pcl-client"),
                VersionName = "missing",
                VersionJson = Json("{}")
            });

        Assert.IsNull(plan.File);
        Assert.AreEqual(MinecraftClientDownloadFailureReason.NoClientJarDownloadInfo, plan.FailureReason);
    }

    [TestMethod]
    public void CreateAssetIndexPlan_ShouldReadAssetIndexDownloadInfo()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-client");

        MinecraftAssetIndexDownloadPlan plan = MinecraftClientDownloadPlanner.CreateAssetIndexPlan(
            new MinecraftAssetIndexDownloadPlanRequest
            {
                MinecraftRootDirectory = root,
                VersionJson = Json("""{"assetIndex":{"id":"17","url":"https://example.test/17.json"}}""")
            });

        Assert.IsTrue(plan.HasDownload);
        Assert.IsFalse(plan.UsedLegacyFallback);
        Assert.AreEqual("17", plan.IndexId);
        Assert.AreEqual("https://example.test/17.json", plan.Url);
        Assert.AreEqual(Path.Combine(root, "assets", "indexes", "17.json"), plan.LocalPath);
    }

    [TestMethod]
    public void CreateAssetIndexPlan_ShouldUseLegacyFallback()
    {
        MinecraftAssetIndexDownloadPlan plan = MinecraftClientDownloadPlanner.CreateAssetIndexPlan(
            new MinecraftAssetIndexDownloadPlanRequest
            {
                MinecraftRootDirectory = Path.Combine(Path.GetTempPath(), "pcl-client"),
                VersionJson = Json("{}")
            });

        Assert.IsTrue(plan.HasDownload);
        Assert.IsTrue(plan.UsedLegacyFallback);
        Assert.AreEqual(MinecraftAssetIndexResolver.LegacyIndexName, plan.IndexId);
        Assert.AreEqual(MinecraftAssetIndexResolver.LegacyIndexUrl, plan.Url);
    }

    [TestMethod]
    public void CreateAssetIndexPlan_ShouldKeepIndexIdWhenUrlIsMissing()
    {
        MinecraftAssetIndexDownloadPlan plan = MinecraftClientDownloadPlanner.CreateAssetIndexPlan(
            new MinecraftAssetIndexDownloadPlanRequest
            {
                MinecraftRootDirectory = Path.Combine(Path.GetTempPath(), "pcl-client"),
                VersionJson = Json("""{"assetIndex":{"id":"custom"}}""")
            });

        Assert.IsFalse(plan.HasDownload);
        Assert.AreEqual("custom", plan.IndexId);
        Assert.IsNull(plan.Url);
    }

    private static JsonObject Json(string value) => JsonNode.Parse(value)!.AsObject();
}
