// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Application.Instances;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftJarPatchServiceTests
{
    [TestMethod]
    public async Task ApplyAsync_MergesPatchArchiveIntoTargetJar()
    {
        string root = Path.Combine(Path.GetTempPath(), "pcl-jar-patch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string target = Path.Combine(root, "client.jar");
        string patch = Path.Combine(root, "patch.zip");

        try
        {
            CreateArchive(target, ("assets/old.txt", "old"));
            CreateArchive(
                patch,
                ("assets/old.txt", "new"),
                ("assets/new.txt", "added"),
                ("META-INF/signature.sf", "skip"));

            int patched = await MinecraftJarPatchService.ApplyAsync(
                new MinecraftJarPatchRequest
                {
                    TargetJarPath = target,
                    PatchArchivePath = patch
                });

            Assert.AreEqual(2, patched);
            using ZipArchive archive = ZipFile.OpenRead(target);
            Assert.AreEqual("new", ReadEntry(archive, "assets/old.txt"));
            Assert.AreEqual("added", ReadEntry(archive, "assets/new.txt"));
            Assert.IsNull(archive.GetEntry("META-INF/signature.sf"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateArchive(string path, params (string Name, string Content)[] entries)
    {
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string name, string content) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(name);
            using StreamWriter writer = new(entry.Open());
            writer.Write(content);
        }
    }

    private static string? ReadEntry(ZipArchive archive, string name)
    {
        ZipArchiveEntry? entry = archive.GetEntry(name);
        if (entry is null)
            return null;

        using StreamReader reader = new(entry.Open());
        return reader.ReadToEnd();
    }
}
