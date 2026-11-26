using System.IO;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public class ModpackArchiveTest
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory();

    [TestCleanup]
    public void Cleanup() => _root.Delete(true);

    [TestMethod]
    [DataRow("原始整合包.zip", false)]
    [DataRow("原始整合包.mrpack", true)]
    [DataRow("本地自选.zip", true)]
    public void InstallPreparationKeepsArchiveReadable(string name, bool isInput)
    {
        var folder = _root.CreateSubdirectory("Pack").FullName;
        var path = Path.Combine(folder, name);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create)) zip.CreateEntry("marker.txt");
        var residue = Path.Combine(folder, ".pclignore");
        File.WriteAllText(residue, "");
        McInstallRecovery.TryClearIncompleteInstanceFolder(folder, "Pack", isInput ? path : null);
        Assert.IsFalse(File.Exists(residue));
        using var archive = ZipFile.OpenRead(path);
        Assert.IsNotNull(archive.GetEntry("marker.txt"));
    }
}
