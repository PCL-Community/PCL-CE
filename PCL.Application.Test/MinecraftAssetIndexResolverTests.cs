// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Assets;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftAssetIndexResolverTests
{
    [TestMethod]
    public void ResolveIndex_ShouldUseAssetIndexWithId()
    {
        MinecraftAssetIndexResolution resolution = MinecraftAssetIndexResolver.ResolveIndex(
            new MinecraftAssetIndexRequest
            {
                VersionJson = Json("""{"assetIndex":{"id":"17","url":"https://example.test/17.json"}}""")
            });

        Assert.IsNotNull(resolution.IndexJson);
        Assert.IsFalse(resolution.UsedLegacyFallback);
        Assert.AreEqual("17", resolution.IndexJson["id"]!.ToString());
    }

    [TestMethod]
    public void ResolveIndex_ShouldAllowUrlOnlyAssetIndexWhenRequested()
    {
        MinecraftAssetIndexResolution resolution = MinecraftAssetIndexResolver.ResolveIndex(
            new MinecraftAssetIndexRequest
            {
                AllowUrlOnlyAssetIndex = true,
                VersionJson = Json("""{"assetIndex":{"url":"https://example.test/custom.json"}}""")
            });

        Assert.IsNotNull(resolution.IndexJson);
        Assert.AreEqual("https://example.test/custom.json", resolution.IndexJson["url"]!.ToString());
    }

    [TestMethod]
    public void ResolveIndex_ShouldFallbackToLegacyWhenRequested()
    {
        MinecraftAssetIndexResolution resolution = MinecraftAssetIndexResolver.ResolveIndex(
            new MinecraftAssetIndexRequest
            {
                UseLegacyFallback = true,
                VersionJson = Json("{}")
            });

        Assert.IsNotNull(resolution.IndexJson);
        Assert.IsTrue(resolution.UsedLegacyFallback);
        Assert.AreEqual(MinecraftAssetIndexResolver.LegacyIndexName, resolution.IndexJson["id"]!.ToString());
        Assert.AreEqual(MinecraftAssetIndexResolver.LegacyIndexUrl, resolution.IndexJson["url"]!.ToString());
    }

    [TestMethod]
    public void ResolveIndex_ShouldReturnNullWhenMissingWithoutFallback()
    {
        MinecraftAssetIndexResolution resolution = MinecraftAssetIndexResolver.ResolveIndex(
            new MinecraftAssetIndexRequest
            {
                VersionJson = Json("{}")
            });

        Assert.IsNull(resolution.IndexJson);
        Assert.IsFalse(resolution.UsedLegacyFallback);
    }

    [TestMethod]
    public void GetIndexName_ShouldPreferAssetIndexId()
    {
        string indexName = MinecraftAssetIndexResolver.GetIndexName(
            new MinecraftAssetIndexNameRequest
            {
                VersionJson = Json("""{"assetIndex":{"id":"17"},"assets":"legacy"}""")
            });

        Assert.AreEqual("17", indexName);
    }

    [TestMethod]
    public void GetIndexName_ShouldUseAssetsWhenAssetIndexMissing()
    {
        string indexName = MinecraftAssetIndexResolver.GetIndexName(
            new MinecraftAssetIndexNameRequest
            {
                VersionJson = Json("""{"assets":"pre-1.6"}""")
            });

        Assert.AreEqual("pre-1.6", indexName);
    }

    [TestMethod]
    public void GetIndexName_ShouldUseInheritedVersionWhenCurrentHasNoIndex()
    {
        string indexName = MinecraftAssetIndexResolver.GetIndexName(
            new MinecraftAssetIndexNameRequest
            {
                VersionJson = Json("{}"),
                InheritedVersionJsons =
                [
                    Json("{}"),
                    Json("""{"assetIndex":{"id":"1.20"}}""")
                ]
            });

        Assert.AreEqual("1.20", indexName);
    }

    [TestMethod]
    public void GetIndexName_ShouldFallbackToLegacy()
    {
        string indexName = MinecraftAssetIndexResolver.GetIndexName(
            new MinecraftAssetIndexNameRequest
            {
                VersionJson = Json("{}"),
                InheritedVersionJsons = [Json("{}")]
            });

        Assert.AreEqual(MinecraftAssetIndexResolver.LegacyIndexName, indexName);
    }

    private static JsonObject Json(string value) => JsonNode.Parse(value)!.AsObject();
}
