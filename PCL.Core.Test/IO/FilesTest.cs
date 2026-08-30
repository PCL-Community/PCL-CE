using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
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
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "PCLCoreFilesTest",
            Guid.NewGuid().ToString("N"));
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
        await Files.ExtractFileAsync(
            archivePath,
            outputPath,
            progress.Add,
            encoding,
            TestContext.CancellationToken);

        var extractedFile = Path.Combine(outputPath, "overrides", "中文.txt");
        Assert.IsTrue(File.Exists(extractedFile));
        Assert.AreEqual(
            "ok",
            await File.ReadAllTextAsync(extractedFile, TestContext.CancellationToken));
        Assert.HasCount(1, progress);
        Assert.AreEqual(1d, progress[0], 0.0000001d);
    }

    [TestMethod]
    public async Task ExtractFileAsyncAcceptsZipArchiveExtensionsCaseInsensitively()
    {
        var encoding = Encoding.GetEncoding("GB18030");

        foreach (var fileName in new[] { "PACK.ZIP", "foo.JAR", "pack.MRPACK" })
        {
            var archivePath = Path.Combine(_tempDir, fileName);
            var outputPath = Path.Combine(
                _tempDir,
                Path.GetFileNameWithoutExtension(fileName) + "-out");

            await using (var archive = await ZipFile.OpenAsync(
                             archivePath,
                             ZipArchiveMode.Create,
                             encoding, TestContext.CancellationToken))
            {
                var entry = archive.CreateEntry("overrides/uppercase-extension.txt");
                await using var stream = await entry.OpenAsync(TestContext.CancellationToken);
                await using var writer = new StreamWriter(stream, Encoding.UTF8);
                await writer.WriteAsync("ok");
            }

            await Files.ExtractFileAsync(
                archivePath,
                outputPath,
                null,
                encoding,
                TestContext.CancellationToken);

            var extractedFile = Path.Combine(outputPath, "overrides", "uppercase-extension.txt");
            Assert.IsTrue(File.Exists(extractedFile), $"Failed to extract {fileName}");
            Assert.AreEqual(
                "ok",
                await File.ReadAllTextAsync(extractedFile, TestContext.CancellationToken));
        }
    }

    [TestMethod]
    public async Task ExtractFileAsyncExtractsTarArchiveSuccessfully()
    {
        var archivePath = Path.Combine(_tempDir, "test.tar");
        var outputPath = Path.Combine(_tempDir, "tar_out");

        await using (var fs = File.Create(archivePath))
        await using (var tarOutput = new TarOutputStream(fs, Encoding.UTF8))
        {
            var content = "hello tar"u8.ToArray();
            var entry = TarEntry.CreateTarEntry("folder/test.txt");
            entry.Size = content.Length;
            tarOutput.PutNextEntry(entry);
            await tarOutput.WriteAsync(content, TestContext.CancellationToken);
            tarOutput.CloseEntry();
        }

        var progress = new List<double>();
        await Files.ExtractFileAsync(
            archivePath,
            outputPath,
            progress.Add,
            Encoding.UTF8,
            TestContext.CancellationToken);

        var extractedFile = Path.Combine(outputPath, "folder", "test.txt");
        Assert.IsTrue(File.Exists(extractedFile));
        Assert.AreEqual(
            "hello tar",
            await File.ReadAllTextAsync(extractedFile, TestContext.CancellationToken));
        Assert.HasCount(1, progress);
        Assert.AreEqual(1d, progress[0], 0.0000001d);
    }

    [TestMethod]
    public async Task CopyFileAsyncDoesNotTruncateWhenPathsDifferOnlyInCasing()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "important content", TestContext.CancellationToken);

        var samePathDifferentCasing = Path.Combine(_tempDir, "TEST.TXT");
        await Files.CopyFileAsync(filePath, samePathDifferentCasing, TestContext.CancellationToken);

        Assert.AreEqual(
            "important content",
            await File.ReadAllTextAsync(filePath, TestContext.CancellationToken));
    }
}