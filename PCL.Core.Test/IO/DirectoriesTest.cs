using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.IO;

[TestClass]
public class DirectoriesTest
{
    private string _tempDir = null!;

    public required TestContext TestContext { get; set; }

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PCLCoreDirectoriesTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public async Task CheckPermissionAsyncUsesRealProbeFile()
    {
        Assert.IsTrue(await Directories.CheckPermissionAsync(_tempDir, TestContext.CancellationToken));
        Assert.AreEqual(0, Directory.EnumerateFiles(_tempDir, ".pcl-permission-*.tmp").Count());
    }

    [TestMethod]
    public async Task CheckPermissionAsyncReturnsFalseForMissingDirectory()
    {
        var missing = Path.Combine(_tempDir, "missing");
        Assert.IsFalse(await Directories.CheckPermissionAsync(missing, TestContext.CancellationToken));
    }

    [TestMethod]
    public async Task CopyDirectoryAsyncReportsIncrementalProgress()
    {
        var source = Path.Combine(_tempDir, "source");
        var target = Path.Combine(_tempDir, "target");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(Path.Combine(source, "one.txt"), "1", TestContext.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(source, "two.txt"), "2", TestContext.CancellationToken);

        var progress = new List<double>();
        await Directories.CopyDirectoryAsync(source, target, progress.Add, TestContext.CancellationToken);

        Assert.HasCount(2, progress);
        Assert.AreEqual(0.5d, progress[0], 0.0000001d);
        Assert.AreEqual(0.5d, progress[1], 0.0000001d);
        Assert.IsTrue(File.Exists(Path.Combine(target, "one.txt")));
        Assert.IsTrue(File.Exists(Path.Combine(target, "two.txt")));
    }
}