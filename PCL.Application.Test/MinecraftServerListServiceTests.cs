// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using fNbt;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Instances;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftServerListServiceTests
{
    [TestMethod]
    public async Task LoadAsync_ReadsServersDatEntries()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-server-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            WriteServersDat(root);
            IReadOnlyList<MinecraftServerEntry> servers = await MinecraftServerListService.LoadAsync(root);

            Assert.AreEqual(2, servers.Count);
            Assert.AreEqual("Hypixel", servers[0].Name);
            Assert.AreEqual("mc.hypixel.net", servers[0].Address);
            Assert.AreEqual("Local", servers[1].Name);
            Assert.AreEqual("127.0.0.1", servers[1].Address);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_MissingServersDatReturnsEmptyList()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-server-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            IReadOnlyList<MinecraftServerEntry> servers = await MinecraftServerListService.LoadAsync(root);

            Assert.AreEqual(0, servers.Count);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task AddAsync_AppendsServerDatEntry()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-server-list-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            WriteServersDat(root);

            await MinecraftServerListService.AddAsync(
                root,
                new MinecraftServerEntry("Example", "play.example.net", null));

            IReadOnlyList<MinecraftServerEntry> servers = await MinecraftServerListService.LoadAsync(root);

            Assert.AreEqual(3, servers.Count);
            Assert.AreEqual("Example", servers[2].Name);
            Assert.AreEqual("play.example.net", servers[2].Address);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteServersDat(string root)
    {
        NbtCompound rootTag = new("");
        NbtList servers = new("servers", NbtTagType.Compound)
        {
            new NbtCompound
            {
                new NbtString("name", "Hypixel"),
                new NbtString("ip", "mc.hypixel.net")
            },
            new NbtCompound
            {
                new NbtString("name", "Local"),
                new NbtString("ip", "127.0.0.1")
            }
        };
        rootTag.Add(servers);

        NbtFile file = new(rootTag);
        using FileStream stream = File.Create(Path.Combine(root, "servers.dat"));
        file.SaveToStream(stream, NbtCompression.GZip);
    }
}
