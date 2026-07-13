// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO.Compression;
using System.Text;
using PCL.Application.Downloads;

namespace PCL.Application.Test;

[TestClass]
public sealed class MinecraftWorldArchiveInstallerTests
{
    [TestMethod]
    public async Task InstallAsync_ShouldStripWrapperDirectoryAndInstallWorld()
    {
        using TemporaryDirectory temp = new();
        string archive = CreateArchive(temp.Path, "Adventure.zip", new Dictionary<string, string>
        {
            ["Adventure World/level.dat"] = "level",
            ["Adventure World/region/r.0.0.mca"] = "region",
            ["__MACOSX/._level.dat"] = "junk"
        });

        string installed = await MinecraftWorldArchiveInstaller.InstallAsync(archive, Path.Combine(temp.Path, "saves"));

        Assert.AreEqual("Adventure World", Path.GetFileName(installed));
        Assert.IsTrue(File.Exists(Path.Combine(installed, "level.dat")));
        Assert.IsTrue(File.Exists(Path.Combine(installed, "region", "r.0.0.mca")));
        Assert.IsFalse(Directory.Exists(Path.Combine(installed, "__MACOSX")));
    }

    [TestMethod]
    public async Task InstallAsync_ShouldCreateUniqueDestination()
    {
        using TemporaryDirectory temp = new();
        string saves = Path.Combine(temp.Path, "saves");
        Directory.CreateDirectory(Path.Combine(saves, "World"));
        string archive = CreateArchive(temp.Path, "World.zip", new Dictionary<string, string>
        {
            ["World/level.dat"] = "level"
        });

        string installed = await MinecraftWorldArchiveInstaller.InstallAsync(archive, saves);

        Assert.AreEqual("World (2)", Path.GetFileName(installed));
    }

    [TestMethod]
    public async Task InstallAsync_ShouldRejectTraversalEntries()
    {
        using TemporaryDirectory temp = new();
        string archivePath = Path.Combine(temp.Path, "unsafe.zip");
        using (FileStream stream = File.Create(archivePath))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "World/level.dat", "level");
            WriteEntry(archive, "World/../escaped.txt", "escaped");
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            MinecraftWorldArchiveInstaller.InstallAsync(archivePath, Path.Combine(temp.Path, "saves")));
        Assert.IsFalse(File.Exists(Path.Combine(temp.Path, "escaped.txt")));
    }

    [TestMethod]
    public async Task InstallAsync_ShouldRejectArchiveWithoutLevelDat()
    {
        using TemporaryDirectory temp = new();
        string archive = CreateArchive(temp.Path, "not-world.zip", new Dictionary<string, string>
        {
            ["readme.txt"] = "hello"
        });

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            MinecraftWorldArchiveInstaller.InstallAsync(archive, Path.Combine(temp.Path, "saves")));
    }

    private static string CreateArchive(string directory, string name, IReadOnlyDictionary<string, string> entries)
    {
        string path = Path.Combine(directory, name);
        using FileStream stream = File.Create(path);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        foreach (KeyValuePair<string, string> entry in entries)
            WriteEntry(archive, entry.Key, entry.Value);
        return path;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path);
        using Stream target = entry.Open();
        byte[] data = Encoding.UTF8.GetBytes(content);
        target.Write(data);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pcln-world-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
