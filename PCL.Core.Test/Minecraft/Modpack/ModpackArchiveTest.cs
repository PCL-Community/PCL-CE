using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.Modpack;

namespace PCL.Core.Test.Minecraft.Modpack;

[TestClass]
public class ModpackArchiveTest
{
    private string? _archivePath;

    [TestCleanup]
    public void Cleanup()
    {
        if (_archivePath is not null && File.Exists(_archivePath)) File.Delete(_archivePath);
    }

    [TestMethod]
    public void FindsAndEnumeratesDirectoriesCaseInsensitively()
    {
        _archivePath = Path.Combine(Path.GetTempPath(), $"pclce-archive-test-{Guid.NewGuid():N}.zip");
        using (var stream = File.Create(_archivePath))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open())) writer.Write("{}");
            using (var writer = new StreamWriter(zip.CreateEntry("Overrides/config/a.cfg").Open())) writer.Write("a");
        }

        using var archive = ModpackArchive.Open(_archivePath);

        Assert.IsTrue(archive.HasDirectory("overrides"));
        var item = archive.EnumerateFiles("overrides").Single();
        Assert.AreEqual("config/a.cfg", item.RelativePath, ignoreCase: true);
    }
}
