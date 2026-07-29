using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack.Installation;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class ModpackJarModMergerTest
{
    private string? _temporaryDirectory;

    [TestCleanup]
    public void Cleanup()
    {
        if (_temporaryDirectory is not null && Directory.Exists(_temporaryDirectory))
            Directory.Delete(_temporaryDirectory, recursive: true);
    }

    [TestMethod]
    public void AppliesJarModsInOrderAndPreservesOtherEntries()
    {
        var directory = _CreateDirectory();
        var gameJar = _CreateJar(directory, "game.jar", new Dictionary<string, string>
        {
            ["base.txt"] = "base",
            ["replace.txt"] = "original"
        });
        var first = _CreateJar(directory, "first.jar", new Dictionary<string, string>
        {
            ["replace.txt"] = "first",
            ["first.txt"] = "one"
        });
        var second = _CreateJar(directory, "second.jar", new Dictionary<string, string>
        {
            ["replace.txt"] = "second"
        });

        ModpackJarModMerger.Merge(gameJar, [first, second]);

        Assert.AreEqual("base", _ReadEntry(gameJar, "base.txt"));
        Assert.AreEqual("one", _ReadEntry(gameJar, "first.txt"));
        Assert.AreEqual("second", _ReadEntry(gameJar, "replace.txt"));
        using var archive = ZipFile.OpenRead(gameJar);
        Assert.AreEqual(1, archive.Entries.Count(entry => entry.FullName == "replace.txt"));
    }

    [TestMethod]
    public void LeavesGameJarUnchangedWhenJarModIsInvalid()
    {
        var directory = _CreateDirectory();
        var gameJar = _CreateJar(directory, "game.jar", new Dictionary<string, string>
        {
            ["base.txt"] = "base"
        });
        var invalid = Path.Combine(directory, "invalid.jar");
        File.WriteAllText(invalid, "not a zip");
        var before = File.ReadAllBytes(gameJar);

        Assert.ThrowsExactly<InvalidDataException>(() => ModpackJarModMerger.Merge(gameJar, [invalid]));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(gameJar));
    }

    private string _CreateDirectory()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"pclce-jarmod-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        return _temporaryDirectory;
    }

    private static string _CreateJar(string directory, string name, Dictionary<string, string> entries)
    {
        var path = Path.Combine(directory, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            using var writer = new StreamWriter(archive.CreateEntry(entryName).Open());
            writer.Write(content);
        }

        return path;
    }

    private static string _ReadEntry(string archivePath, string entryName)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        using var reader = new StreamReader(archive.GetEntry(entryName)!.Open());
        return reader.ReadToEnd();
    }
}
