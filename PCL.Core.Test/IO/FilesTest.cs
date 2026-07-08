using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO;

namespace PCL.Core.Test.IO;

[TestClass]
public class FilesTest
{
    private string _tempDir = null!;

    public required TestContext TestContext { get; set; }

    [TestInitialize]
    public void SetUp()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _tempDir = Path.Combine(Path.GetTempPath(), "PCLCoreFilesTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public async Task ExtractFileAsyncTreatsMrpackAsZipAndUsesArchiveEncoding()
    {
        var archivePath = Path.Combine(_tempDir, "pack.mrpack");
        var outputPath = Path.Combine(_tempDir, "out");
        var encoding = Encoding.GetEncoding("GB18030");

        await using (var archive = await ZipFile.OpenAsync(
                         archivePath,
                         ZipArchiveMode.Create,
                         encoding, TestContext.CancellationToken))
        {
            var entry = archive.CreateEntry("overrides/中文.txt");
            await using var stream = await entry.OpenAsync(TestContext.CancellationToken);
            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteAsync("ok");
        }

        var progress = new List<double>();
        await Files.ExtractFileAsync(archivePath, outputPath, progress.Add, encoding, TestContext.CancellationToken);

        var extractedFile = Path.Combine(outputPath, "overrides", "中文.txt");
        Assert.IsTrue(File.Exists(extractedFile));
        Assert.AreEqual("ok", await File.ReadAllTextAsync(extractedFile, TestContext.CancellationToken));
        Assert.HasCount(1, progress);
        Assert.AreEqual(1d, progress[0], 0.0000001d);
    }
}