// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Portable.Test;

[TestClass]
public sealed class FileSystemServiceTests
{
    [TestMethod]
    public async Task EnumeratesAndArchivesFilesAsynchronously()
    {
        var root = Path.Combine(Path.GetTempPath(), "PCL.Core.Portable.Test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first = Path.Combine(root, "first.log");
            var second = Path.Combine(root, "second.log");
            await File.WriteAllTextAsync(first, "first");
            await File.WriteAllTextAsync(second, "second");

            var snapshots = await FileSystemService.GetFilesAsync(root, "*.log");
            Assert.HasCount(2, snapshots);

            var archivePath = Path.Combine(root, "logs.zip");
            await FileSystemService.CreateZipAsync(
                archivePath,
                snapshots.Select(file => file.FullPath));

            using var archive = ZipFile.OpenRead(archivePath);
            CollectionAssert.AreEquivalent(
                new[] { "first.log", "second.log" },
                archive.Entries.Select(entry => entry.FullName).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
