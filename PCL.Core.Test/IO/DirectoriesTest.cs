using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.IO;

[TestClass]
public sealed class DirectoriesTest
{
    private string _root = string.Empty;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "pcl-directories-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [TestMethod]
    public void EnsureWritableAcceptsExistingDirectory()
    {
        Directories.EnsureWritable(_root);
        Assert.IsTrue(Directories.CanWrite(_root));
    }

    [TestMethod]
    public async Task EnsureWritableAsyncAcceptsExistingDirectory()
    {
        await Directories.EnsureWritableAsync(_root);
        Assert.IsTrue(await Directories.CanWriteAsync(_root));
    }

    [TestMethod]
    public void MissingDirectoryIsRejected()
    {
        var path = Path.Combine(_root, "missing");

        Assert.IsFalse(Directories.CanWrite(path));
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => Directories.EnsureWritable(path));
    }

    [TestMethod]
    public void DeleteDirectoryIsRecursiveAndIdempotent()
    {
        var child = Path.Combine(_root, "child");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(child, "file.txt"), "x");

        Directories.DeleteDirectory(_root);
        Assert.IsFalse(Directory.Exists(_root));

        Directories.DeleteDirectory(_root);
    }

    [TestMethod]
    public void PermissionApiDoesNotDeclareWindowsOnlySupport()
    {
        foreach (var method in typeof(Directories).GetMethods())
            Assert.IsFalse(
                Attribute.IsDefined(method, typeof(SupportedOSPlatformAttribute)),
                $"{method.Name} 不应被标记为 Windows-only");
    }
}