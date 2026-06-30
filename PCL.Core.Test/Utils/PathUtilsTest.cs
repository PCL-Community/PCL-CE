using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.Utils;

[TestClass]
public class PathUtilsTest
{
    [TestMethod]
    [DataRow("https://example.com/files/core.jar?download=1", "core.jar")]
    [DataRow(@"C:\\Games\\Minecraft\\core.jar", "core.jar")]
    [DataRow("/tmp/a/b/core.jar", "core.jar")]
    public void GetsFileNameFromUrlOrPath(string path, string expected)
    {
        Assert.AreEqual(expected, PathUtils.GetFileNameFromUrlOrPath(path));
    }

    [TestMethod]
    [DataRow(@"C:\\Games\\Minecraft\\", "Minecraft")]
    [DataRow(@"D:\\", "D")]
    public void GetsDirectoryLeaf(string path, string expected)
    {
        Assert.AreEqual(expected, PathUtils.GetDirectoryNameLeaf(path));
    }
}