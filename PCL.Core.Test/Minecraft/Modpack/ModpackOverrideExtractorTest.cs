using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;
using PCL.Core.Minecraft.Modpack.Installation;
using PCL.Core.Minecraft.Modpack.Persistence;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class ModpackOverrideExtractorTest
{
    private readonly List<string> _temporaryPaths = [];

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var path in _temporaryPaths)
        {
            if (File.Exists(path)) File.Delete(path);
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeletesUnmodifiedFileRemovedByUpdate()
    {
        var (archivePath, instanceDirectory) = _CreateScenario("pack-owned");
        var stalePath = Path.Combine(instanceDirectory, "config", "removed.cfg");
        var previous = _Previous("config/removed.cfg", _Sha1("pack-owned"));

        using var archive = ModpackArchive.Open(archivePath);
        await ModpackOverrideExtractor.ExtractAsync(archive, [], instanceDirectory, previous: previous);

        Assert.IsFalse(File.Exists(stalePath));
    }

    [TestMethod]
    public async Task PreservesUserEditedFileRemovedByUpdate()
    {
        var (archivePath, instanceDirectory) = _CreateScenario("user-edited");
        var stalePath = Path.Combine(instanceDirectory, "config", "removed.cfg");
        var previous = _Previous("config/removed.cfg", _Sha1("pack-owned"));

        using var archive = ModpackArchive.Open(archivePath);
        await ModpackOverrideExtractor.ExtractAsync(archive, [], instanceDirectory, previous: previous);

        Assert.IsTrue(File.Exists(stalePath));
        Assert.AreEqual("user-edited", File.ReadAllText(stalePath));
    }

    private (string ArchivePath, string InstanceDirectory) _CreateScenario(string currentContent)
    {
        var archivePath = Path.Combine(Path.GetTempPath(), $"pclce-override-test-{Guid.NewGuid():N}.zip");
        var instanceDirectory = Path.Combine(Path.GetTempPath(), $"pclce-override-test-{Guid.NewGuid():N}");
        _temporaryPaths.Add(archivePath);
        _temporaryPaths.Add(instanceDirectory);

        using (var stream = File.Create(archivePath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open()))
            writer.Write("{}");

        Directory.CreateDirectory(Path.Combine(instanceDirectory, "config"));
        File.WriteAllText(Path.Combine(instanceDirectory, "config", "removed.cfg"), currentContent);
        return (archivePath, instanceDirectory);
    }

    private static ModpackConfiguration _Previous(string path, string hash) => new()
    {
        Overrides = [new ModpackFileSnapshot(path, hash)]
    };

    private static string _Sha1(string content)
        => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(content)));
}
