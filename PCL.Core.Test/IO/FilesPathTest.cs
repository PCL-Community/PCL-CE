using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.IO;

[TestClass]
public sealed class FilesPathTest
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "pcl-files-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [TestMethod]
    public void PathWithinDirectoryRejectsSiblingAndParent()
    {
        var baseDir = Path.Combine(_root, "base");

        Assert.IsTrue(Files.IsPathWithinDirectory(Path.Combine(baseDir, "a", "b.txt"), baseDir));
        Assert.IsFalse(Files.IsPathWithinDirectory(Path.Combine(_root, "base2", "b.txt"), baseDir));
        Assert.IsFalse(Files.IsPathWithinDirectory(Path.Combine(baseDir, "..", "b.txt"), baseDir));
    }

    [TestMethod]
    public void PathWithinDirectoryFollowsPlatformCaseRule()
    {
        var baseDir = Path.Combine(_root, "base");
        var child = Path.Combine(_root, "BASE", "b.txt");

        Assert.AreEqual(OperatingSystem.IsWindows(), Files.IsPathWithinDirectory(child, baseDir));
    }

    [TestMethod]
    public void ExtractZipRejectsEntryOutsideDestination()
    {
        var zipPath = Path.Combine(_root, "evil.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            archive.CreateEntry("../dest2/evil.txt");
        var dest = Path.Combine(_root, "dest");

        Assert.ThrowsExactly<Exception>(() => Files.ExtractFile(zipPath, dest, Encoding.UTF8));
        Assert.IsFalse(File.Exists(Path.Combine(_root, "dest2", "evil.txt")));
    }
}
