using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.Net.Downloader.Network;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Test.Downloader;

[TestClass]
public class SpeedMonitorTests
{
    [TestMethod]
    public async Task SpeedMonitor_ShouldCancel_WhenSpeedIsTooLow()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        double minSpeedThreshold = 1000; // 1000 Bytes/s
        var checkInterval = TimeSpan.FromMilliseconds(200);

        // Act
        await using (var monitor = new SpeedMonitor(cts, minSpeedThreshold, checkInterval, TimeSpan.Zero))
        {
            // 模拟极低的下载速度：200ms 内只下载了 10 bytes (相当于 50 Bytes/s)
            monitor.ReportBytesRead(10);

            await Task.Delay(400);
        }

        // Assert
        Assert.IsTrue(cts.IsCancellationRequested, "速度低于阈值，应当触发 Cancel()");
    }

    [TestMethod]
    public async Task SpeedMonitor_ShouldNotCancel_WhenSpeedIsHighEnough()
    {
        // Arrange
        using var targetCts = new CancellationTokenSource();
        double minSpeedThreshold = 1000; // 要求最低 1000 Bytes/s
        var checkInterval = TimeSpan.FromMilliseconds(200); // 200ms 检查一次

        // Act
        await using (var monitor =
                     new SpeedMonitor(targetCts, minSpeedThreshold, checkInterval, gracePeriod: TimeSpan.Zero))
        {
            // 模拟持续且稳定的高速下载
            // 持续 500ms，每隔 100ms 塞入 2000 Bytes
            for (int i = 0; i < 5; i++)
            {
                monitor.ReportBytesRead(2000);
                await Task.Delay(100);
            }
        }

        // Assert
        Assert.IsFalse(targetCts.IsCancellationRequested, "在持续的高速下载中，目标 CancellationToken 绝对不应被取消");
    }
}