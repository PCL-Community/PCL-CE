using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft;

namespace PCL.Core.Test.Minecraft;

[TestClass]
public class SaveImportHelperTest
{
    private string _tempDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PCLTest", "WorldImportHelper", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }

    [TestMethod]
    public void GetSaveRootDirectoryUsesExtractedRootWhenLevelDatIsAtRoot()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "level.dat"), "");

        var result = SaveImportHelper.GetSaveRootDirectory(_tempDirectory);

        Assert.AreEqual(Path.GetFullPath(_tempDirectory), result);
    }

    [TestMethod]
    public void GetSaveRootDirectoryFlattensSingleTopLevelFolderWithLevelDat()
    {
        var worldDirectory = Path.Combine(_tempDirectory, "MyWorld");
        Directory.CreateDirectory(worldDirectory);
        File.WriteAllText(Path.Combine(worldDirectory, "level.dat"), "");

        var result = SaveImportHelper.GetSaveRootDirectory(_tempDirectory);

        Assert.AreEqual(Path.GetFullPath(worldDirectory), result);
    }

    [TestMethod]
    public void GetSaveRootDirectoryFlattensSingleTopLevelFolderEvenWithRootFiles()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "thumbs.db"), "");
        var worldDirectory = Path.Combine(_tempDirectory, "MyWorld");
        Directory.CreateDirectory(worldDirectory);
        File.WriteAllText(Path.Combine(worldDirectory, "level.dat"), "");

        var result = SaveImportHelper.GetSaveRootDirectory(_tempDirectory);

        Assert.AreEqual(Path.GetFullPath(worldDirectory), result);
    }

    [TestMethod]
    public void GetSaveRootDirectoryRejectsSingleTopLevelFolderWithoutLevelDat()
    {
        var worldDirectory = Path.Combine(_tempDirectory, "MyWorld");
        Directory.CreateDirectory(worldDirectory);
        File.WriteAllText(Path.Combine(worldDirectory, "readme.txt"), "");

        var result = SaveImportHelper.GetSaveRootDirectory(_tempDirectory);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetSaveRootDirectoryRejectsMultipleTopLevelFolders()
    {
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "MyWorld"));
        Directory.CreateDirectory(Path.Combine(_tempDirectory, "OtherWorld"));
        File.WriteAllText(Path.Combine(_tempDirectory, "MyWorld", "level.dat"), "");

        var result = SaveImportHelper.GetSaveRootDirectory(_tempDirectory);

        Assert.IsNull(result);
    }
}
