using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PCL.Core.Test;

[TestClass]
public sealed class GameFoldersTest
{
    [TestMethod]
    public void ApplySelectedFolderExpandsPlaceholderAndIgnoresEmpty()
    {
        var previous = GameFolders.mcFolderSelected;
        try
        {
            GameFolders.ApplySelectedFolder("$.minecraft" + Path.DirectorySeparatorChar);
            Assert.AreEqual(
                Path.Combine(LauncherPaths.ExecutableDirectory, ".minecraft")
                + Path.DirectorySeparatorChar,
                GameFolders.mcFolderSelected);

            var custom = Path.Combine(Path.GetTempPath(), "pcl-test-custom-folder")
                + Path.DirectorySeparatorChar;
            GameFolders.ApplySelectedFolder(custom);
            Assert.AreEqual(custom, GameFolders.mcFolderSelected);

            GameFolders.ApplySelectedFolder("  ");
            Assert.AreEqual(custom, GameFolders.mcFolderSelected);
        }
        finally
        {
            GameFolders.mcFolderSelected = previous;
        }
    }
}
