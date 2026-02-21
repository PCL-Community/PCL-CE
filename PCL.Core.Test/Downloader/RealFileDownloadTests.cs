using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Download;
using PCL.Core.IO.Download.Core;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Test.Downloader;

[TestClass]
public class RealFileDownloadTests
{
    [TestMethod]
    public async Task DownloadFile_ShouldCompleteSuccessfully()
    {
        // Arrange
        var destinationPath = Path.Combine(Path.GetTempPath(), "testFile.exe");

        var options = new DownloadOptions
        (
            ["https://dldir1.qq.com/qqfile/qq/PCQQ9.7.17/QQ9.7.17.29225.exe"],
            destinationPath
        );
        var downloader = new DownloadClient(options);
        downloader.ProgressChanged += (s, e) => { Console.WriteLine($"下载进度: {e.ProgressPercentage}%"); };
        downloader.StateChanged += (s, e) => { Console.WriteLine($"下载状态改变: {e.NewState}"); };
        var ct = CancellationToken.None;
        // Act
        await downloader.StartAsync(ct);
        // Assert
        Assert.IsTrue(File.Exists(destinationPath), "下载完成后文件应该存在");
        var fileInfo = new FileInfo(destinationPath);
        Assert.AreEqual(213578248, fileInfo.Length, "下载的文件大小应该是 213578248 Bytes");

        // Cleanup
        File.Delete(destinationPath);
    }
}