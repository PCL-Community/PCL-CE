using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Download;
using PCL.Core.IO.Download.Core;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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
        var downloader = DownloadService.CreateJob(options, () => new HttpClient());
        downloader.ProgressChanged += (s, e) => { Console.WriteLine($"下载进度: {e.ProgressPercentage}%"); };
        downloader.StateChanged += (s, e) => { Console.WriteLine($"下载状态改变: {e.NewState}"); };

        // Act
        await downloader.StartAsync();

        // Assert
        Assert.IsTrue(File.Exists(destinationPath), "下载完成后文件应该存在");
        var fileInfo = new FileInfo(destinationPath);
        Assert.AreEqual(213578248, fileInfo.Length, "下载的文件大小应该是 213578248 Bytes");

        // Cleanup
        File.Delete(destinationPath);
    }

    // NOTE:
    // This test cannot runing directorily, because Config is not been loaded in Test mode. <see cref="DownloadService._GlobalThrottle">
    [TestMethod]
    public async Task DownloadMultipleSmillFile_ShouldCompleteSccessfully()
    {
        string[] targetUrls =
        [
            "https://libraries.minecraft.net/ca/weblite/java-objc-bridge/1.1/java-objc-bridge-1.1.jar",
            "https://libraries.minecraft.net/com/github/oshi/oshi-core/6.4.5/oshi-core-6.4.5.jar",
            "https://libraries.minecraft.net/com/google/code/gson/gson/2.10.1/gson-2.10.1.jar",
            "https://libraries.minecraft.net/com/google/guava/failureaccess/1.0.1/failureaccess-1.0.1.jar",
            "https://libraries.minecraft.net/com/google/guava/guava/32.1.2-jre/guava-32.1.2-jre.jar",
            "https://libraries.minecraft.net/com/ibm/icu/icu4j/73.2/icu4j-73.2.jar",
            "https://libraries.minecraft.net/com/mojang/authlib/6.0.52/authlib-6.0.52.jar",
            "https://libraries.minecraft.net/com/mojang/brigadier/1.2.9/brigadier-1.2.9.jar",
            "https://libraries.minecraft.net/com/mojang/datafixerupper/6.0.8/datafixerupper-6.0.8.jar",
            "https://libraries.minecraft.net/com/mojang/logging/1.1.1/logging-1.1.1.jar",
            "https://libraries.minecraft.net/com/mojang/patchy/2.2.10/patchy-2.2.10.jar",
            "https://libraries.minecraft.net/com/mojang/text2speech/1.17.9/text2speech-1.17.9.jar",
            "https://libraries.minecraft.net/commons-codec/commons-codec/1.16.0/commons-codec-1.16.0.jar",
            "https://libraries.minecraft.net/commons-io/commons-io/2.13.0/commons-io-2.13.0.jar",
            "https://libraries.minecraft.net/commons-logging/commons-logging/1.2/commons-logging-1.2.jar",
            "https://libraries.minecraft.net/io/netty/netty-buffer/4.1.97.Final/netty-buffer-4.1.97.Final.jar",
            "https://libraries.minecraft.net/io/netty/netty-codec/4.1.97.Final/netty-codec-4.1.97.Final.jar",
            "https://libraries.minecraft.net/io/netty/netty-common/4.1.97.Final/netty-common-4.1.97.Final.jar",
            "https://libraries.minecraft.net/io/netty/netty-handler/4.1.97.Final/netty-handler-4.1.97.Final.jar",
            "https://libraries.minecraft.net/io/netty/netty-resolver/4.1.97.Final/netty-resolver-4.1.97.Final.jar",
            "https://libraries.minecraft.net/io/netty/netty-transport-classes-epoll/4.1.97.Final/netty-transport-classes-epoll-4.1.97.Final.jar",
            "https://libraries.minecraft.net/io/netty/netty-transport-native-epoll/4.1.97.Final/netty-transport-native-epoll-4.1.97.Final-linux-aarch_64.jar",
            "https://libraries.minecraft.net/io/netty/netty-transport-native-epoll/4.1.97.Final/netty-transport-native-epoll-4.1.97.Final-linux-x86_64.jar",
            "https://libraries.minecraft.net/io/netty/netty-transport-native-unix-common/4.1.97.Final/netty-transport-native-unix-common-4.1.97.Final.jar",
            "https://libraries.minecraft.net/io/netty/netty-transport/4.1.97.Final/netty-transport-4.1.97.Final.jar",
            "https://libraries.minecraft.net/it/unimi/dsi/fastutil/8.5.12/fastutil-8.5.12.jar",
            "https://libraries.minecraft.net/net/java/dev/jna/jna-platform/5.13.0/jna-platform-5.13.0.jar",
            "https://libraries.minecraft.net/net/java/dev/jna/jna/5.13.0/jna-5.13.0.jar",
            "https://libraries.minecraft.net/net/sf/jopt-simple/jopt-simple/5.0.4/jopt-simple-5.0.4.jar",
            "https://libraries.minecraft.net/org/apache/commons/commons-compress/1.22/commons-compress-1.22.jar",
            "https://libraries.minecraft.net/org/apache/commons/commons-lang3/3.13.0/commons-lang3-3.13.0.jar",
            "https://libraries.minecraft.net/org/apache/httpcomponents/httpclient/4.5.13/httpclient-4.5.13.jar",
            "https://libraries.minecraft.net/org/apache/httpcomponents/httpcore/4.4.16/httpcore-4.4.16.jar",
            "https://libraries.minecraft.net/org/apache/logging/log4j/log4j-api/2.19.0/log4j-api-2.19.0.jar",
            "https://libraries.minecraft.net/org/apache/logging/log4j/log4j-core/2.19.0/log4j-core-2.19.0.jar",
            "https://libraries.minecraft.net/org/apache/logging/log4j/log4j-slf4j2-impl/2.19.0/log4j-slf4j2-impl-2.19.0.jar",
            "https://libraries.minecraft.net/org/joml/joml/1.10.5/joml-1.10.5.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-glfw/3.3.2/lwjgl-glfw-3.3.2.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-glfw/3.3.2/lwjgl-glfw-3.3.2-natives-linux.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-glfw/3.3.2/lwjgl-glfw-3.3.2-natives-macos.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-glfw/3.3.2/lwjgl-glfw-3.3.2-natives-macos-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-glfw/3.3.2/lwjgl-glfw-3.3.2-natives-windows.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-glfw/3.3.2/lwjgl-glfw-3.3.2-natives-windows-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-glfw/3.3.2/lwjgl-glfw-3.3.2-natives-windows-x86.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-jemalloc/3.3.2/lwjgl-jemalloc-3.3.2.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-jemalloc/3.3.2/lwjgl-jemalloc-3.3.2-natives-linux.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-jemalloc/3.3.2/lwjgl-jemalloc-3.3.2-natives-macos.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-jemalloc/3.3.2/lwjgl-jemalloc-3.3.2-natives-macos-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-jemalloc/3.3.2/lwjgl-jemalloc-3.3.2-natives-windows.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-jemalloc/3.3.2/lwjgl-jemalloc-3.3.2-natives-windows-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-jemalloc/3.3.2/lwjgl-jemalloc-3.3.2-natives-windows-x86.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-openal/3.3.2/lwjgl-openal-3.3.2.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-openal/3.3.2/lwjgl-openal-3.3.2-natives-linux.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-openal/3.3.2/lwjgl-openal-3.3.2-natives-macos.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-openal/3.3.2/lwjgl-openal-3.3.2-natives-macos-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-openal/3.3.2/lwjgl-openal-3.3.2-natives-windows.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-openal/3.3.2/lwjgl-openal-3.3.2-natives-windows-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-openal/3.3.2/lwjgl-openal-3.3.2-natives-windows-x86.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-opengl/3.3.2/lwjgl-opengl-3.3.2.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-opengl/3.3.2/lwjgl-opengl-3.3.2-natives-linux.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-opengl/3.3.2/lwjgl-opengl-3.3.2-natives-macos.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-opengl/3.3.2/lwjgl-opengl-3.3.2-natives-macos-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-opengl/3.3.2/lwjgl-opengl-3.3.2-natives-windows.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-opengl/3.3.2/lwjgl-opengl-3.3.2-natives-windows-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-opengl/3.3.2/lwjgl-opengl-3.3.2-natives-windows-x86.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-stb/3.3.2/lwjgl-stb-3.3.2.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-stb/3.3.2/lwjgl-stb-3.3.2-natives-linux.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-stb/3.3.2/lwjgl-stb-3.3.2-natives-macos.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-stb/3.3.2/lwjgl-stb-3.3.2-natives-macos-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-stb/3.3.2/lwjgl-stb-3.3.2-natives-windows.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-stb/3.3.2/lwjgl-stb-3.3.2-natives-windows-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-stb/3.3.2/lwjgl-stb-3.3.2-natives-windows-x86.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-tinyfd/3.3.2/lwjgl-tinyfd-3.3.2.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-tinyfd/3.3.2/lwjgl-tinyfd-3.3.2-natives-linux.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-tinyfd/3.3.2/lwjgl-tinyfd-3.3.2-natives-macos.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-tinyfd/3.3.2/lwjgl-tinyfd-3.3.2-natives-macos-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-tinyfd/3.3.2/lwjgl-tinyfd-3.3.2-natives-windows.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-tinyfd/3.3.2/lwjgl-tinyfd-3.3.2-natives-windows-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl-tinyfd/3.3.2/lwjgl-tinyfd-3.3.2-natives-windows-x86.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.2/lwjgl-3.3.2.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.2/lwjgl-3.3.2-natives-linux.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.2/lwjgl-3.3.2-natives-macos.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.2/lwjgl-3.3.2-natives-macos-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.2/lwjgl-3.3.2-natives-windows.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.2/lwjgl-3.3.2-natives-windows-arm64.jar",
            "https://libraries.minecraft.net/org/lwjgl/lwjgl/3.3.2/lwjgl-3.3.2-natives-windows-x86.jar",
            "https://libraries.minecraft.net/org/slf4j/slf4j-api/2.0.7/slf4j-api-2.0.7.jar",
            "https://piston-data.mojang.com/v1/objects/bd65e7d2e3c237be76cfbef4c2405033d7f91521/client-1.12.xml"
        ];

        var fileNames = targetUrls.Select(url => url.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1]);

        var tempPath = Path.Combine(Path.GetTempPath(), "PCL-CE_DownloadTest");
        Directory.CreateDirectory(tempPath);

        var savePaths = fileNames.Select(name => Path.Combine(tempPath, name)).ToList();

        var targets = targetUrls.Select((t, i) => new TargetFileInfo(t, savePaths[i]));

        var startTime = TimeSpan.FromMilliseconds(Environment.TickCount64);

        var httpClient = new HttpClient();

        var dataStorage = new ConcurrentBag<TestCsvData>();
        await Parallel.ForEachAsync(targets, async (target, token) =>
        {
            var options = new DownloadOptions([target.Url], target.SavePath, 16);
            var job = DownloadService.CreateJob(options, () => httpClient);

            var jobStartTime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            await job.StartAsync().ConfigureAwait(false);

            var jobEndTime = TimeSpan.FromMilliseconds(Environment.TickCount64);

            var usedTime = jobEndTime - jobStartTime;

            var isSuccess = false;
            if (File.Exists(target.SavePath))
            {
                isSuccess = true;
                File.Delete(target.SavePath);
            }

            var data = new TestCsvData(target.SavePath, target.Url, jobStartTime, jobEndTime, usedTime, isSuccess);

            dataStorage.Add(data);
        }).ConfigureAwait(false);

        Directory.Delete(tempPath, true);

        var tableData = dataStorage.Select(data => data.ToString());
        var sb = new StringBuilder("FileName, Url, StartAt, EndAt, UsedTime, IsSuccess\n");
        foreach (var table in tableData)
        {
            sb.AppendLine(table);
        }

        await File.WriteAllTextAsync("testFile.csv", sb.ToString()).ConfigureAwait(false);
    }

    private record TargetFileInfo(string Url, string SavePath);

    private record TestCsvData(
        string FileName,
        string Url,
        TimeSpan StartAt,
        TimeSpan EndAt,
        TimeSpan UsedTime,
        bool IsSuccess)
    {
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{FileName}, {Url}, {StartAt:g}, {EndAt:g}, {UsedTime:g}, {IsSuccess}";
        }
    }
}