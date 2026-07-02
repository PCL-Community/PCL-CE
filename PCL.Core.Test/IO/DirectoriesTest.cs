using System;
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
        Assert.IsTrue(await Directories.CheckPermissionAsync(_tempDir));
        Assert.AreEqual(0, Directory.EnumerateFiles(_tempDir, ".pcl-permission-*.tmp").Count());
    }

    [TestMethod]
    public async Task CheckPermissionAsyncReturnsFalseForMissingDirectory()
    {
        var missing = Path.Combine(_tempDir, "missing");
        Assert.IsFalse(await Directories.CheckPermissionAsync(missing));
    }
}