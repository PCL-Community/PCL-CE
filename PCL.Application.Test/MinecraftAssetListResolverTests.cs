// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Minecraft.Assets;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftAssetListResolverTests
{
    [TestMethod]
    public void GetAssetList_ShouldMapObjectsToAssetObjectStore()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-assets-root");

        MinecraftAssetToken asset = MinecraftAssetListResolver.GetAssetList(
            new MinecraftAssetListRequest
            {
                MinecraftRootDirectory = root,
                InstanceDirectory = Path.Combine(root, "versions", "1.20.1"),
                IndexJson = Json("""
                {
                  "objects": {
                    "minecraft/sounds/test.ogg": {
                      "hash": "abcdef1234",
                      "size": 42
                    }
                  }
                }
                """)
            })[0];

        Assert.AreEqual(Path.Combine(root, "assets", "objects", "ab", "abcdef1234"), asset.LocalPath);
        Assert.AreEqual("minecraft/sounds/test.ogg", asset.SourcePath);
        Assert.AreEqual("abcdef1234", asset.Hash);
        Assert.AreEqual(42, asset.Size);
    }

    [TestMethod]
    public void GetAssetList_ShouldMapResourcesToInstanceDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-assets-root");
        string instanceDirectory = Path.Combine(root, "versions", "classic");

        MinecraftAssetToken asset = MinecraftAssetListResolver.GetAssetList(
            new MinecraftAssetListRequest
            {
                MinecraftRootDirectory = root,
                InstanceDirectory = instanceDirectory,
                IndexJson = Json("""
                {
                  "map_to_resources": true,
                  "objects": {
                    "music/menu/menu1.ogg": {
                      "hash": "bcdef12345",
                      "size": 64
                    }
                  }
                }
                """)
            })[0];

        Assert.AreEqual(Path.Combine(instanceDirectory, "resources", "music", "menu", "menu1.ogg"), asset.LocalPath);
    }

    [TestMethod]
    public void GetAssetList_ShouldMapVirtualAssetsToLegacyDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-assets-root");

        MinecraftAssetToken asset = MinecraftAssetListResolver.GetAssetList(
            new MinecraftAssetListRequest
            {
                MinecraftRootDirectory = root,
                InstanceDirectory = Path.Combine(root, "versions", "1.7.10"),
                IndexJson = Json("""
                {
                  "virtual": true,
                  "objects": {
                    "icons/icon_16x16.png": {
                      "hash": "cdef123456",
                      "size": 128
                    }
                  }
                }
                """)
            })[0];

        Assert.AreEqual(Path.Combine(root, "assets", "virtual", "legacy", "icons", "icon_16x16.png"), asset.LocalPath);
    }

    [TestMethod]
    public void GetObjectUrl_ShouldUseHashPrefix()
    {
        Assert.AreEqual(
            "https://resources.download.minecraft.net/ab/abcdef1234",
            MinecraftAssetListResolver.GetObjectUrl("abcdef1234"));
    }

    private static JsonObject Json(string value) => JsonNode.Parse(value)!.AsObject();
}
