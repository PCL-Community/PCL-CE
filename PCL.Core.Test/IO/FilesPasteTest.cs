using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.IO;

[TestClass]
public class FilesPasteTest
{
    private string _root = null!;

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "PCLTest",
            "FilesPaste",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [TestMethod]
    public async Task PastePathsCopiesFileAndSkipsExisting()
    {
        var source = Path.Combine(_root, "source.txt");
        var destination = Path.Combine(_root, "dest");
        await File.WriteAllTextAsync(source, "hello", TestContext.CancellationToken);

        var copied = await Files.PastePathsAsync(
            [source],
            destination,
            true,
            false,
            cancelToken: TestContext.CancellationToken);
        Assert.AreEqual(1, copied);
        Assert.AreEqual(
            "hello",
            await File.ReadAllTextAsync(
                Path.Combine(destination, "source.txt"),
                TestContext.CancellationToken));

        var skipped = await Files.PastePathsAsync(
            [source],
            destination,
            true,
            false,
            true,
            TestContext.CancellationToken);
        Assert.AreEqual(0, skipped);
    }

    [TestMethod]
    public async Task PastePathsCopiesDirectory()
    {
        var source = Path.Combine(_root, "world");
        var destination = Path.Combine(_root, "dest");
        Directory.CreateDirectory(source);
        await File.WriteAllTextAsync(
            Path.Combine(source, "level.dat"),
            "data",
            TestContext.CancellationToken);

        var copied = await Files.PastePathsAsync(
            [source],
            destination,
            false,
            true,
            cancelToken: TestContext.CancellationToken);
        Assert.AreEqual(1, copied);
        Assert.IsTrue(File.Exists(Path.Combine(destination, "world", "level.dat")));
    }
}