using System;
using System.IO;
using System.IO.Compression;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Minecraft.CrashAnalysis;

namespace PCL.Core.Test.Minecraft.CrashAnalysis;

[TestClass]
public sealed class CrashInputReaderTests
{
    [TestMethod]
    public void ImportZipWithNestedLogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "pcl-crash-import-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "report.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("nested/latest.log");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("hello");
        }

        var bundle = new CrashInputReader().Read(new CrashAnalysisRequest
        {
            Source = CrashAnalysisSource.ImportedFile,
            ImportedFilePath = zipPath,
            TempDirectory = root
        });

        Assert.HasCount(1, bundle.Documents);
        Assert.AreEqual("hello", bundle.Documents[0].Text);
    }
}