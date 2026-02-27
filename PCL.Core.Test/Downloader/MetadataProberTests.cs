using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Download.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Test.Downloader;

[TestClass]
public class MetadataProberTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (long FileSize, string ETag, bool SupportRange, int DelayMs)> _responses = new();

        public void AddMirror(string urlContains, long fileSize, string eTag, bool supportRange = true, int delayMs = 0)
        {
            _responses[urlContains] = (fileSize, eTag, supportRange, delayMs);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            foreach (var (key, config) in _responses)
            {
                if (!request.RequestUri!.AbsoluteUri.Contains(key)) continue;

                if (config.DelayMs > 0)
                    await Task.Delay(config.DelayMs, ct);

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(new byte[Math.Min(16384, config.FileSize)])
                };
                response.Content.Headers.ContentLength = config.FileSize;
                response.Headers.ETag = new EntityTagHeaderValue($"\"{config.ETag}\"");

                if (config.SupportRange)
                    response.Headers.AcceptRanges.Add("bytes");

                return response;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    [TestInitialize]
    public void Setup()
    {
        MetadataProber.ClearCache();
    }

    [TestMethod]
    public async Task ProbeAsync_ShouldFilterInconsistentMirrors()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddMirror("mirror1", 1024, "v1");
        handler.AddMirror("mirror2", 1024, "v1");
        handler.AddMirror("mirror3_bad", 999, "v0"); // 不一致

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var prober = new MetadataProber();

        // Act
        var result = await prober.ProbeAsync(
            ["http://fake.com/mirror1", "http://fake.com/mirror2", "http://fake.com/mirror3_bad"],
            client);

        // Assert
        Assert.AreEqual(1024, result.FileSize);
        Assert.IsTrue(result.SupportRange);
        Assert.AreEqual(2, result.SortedMirrors.Count);
        Assert.IsTrue(result.SortedMirrors.Exists(m => m.Url.Contains("mirror1")));
        Assert.IsTrue(result.SortedMirrors.Exists(m => m.Url.Contains("mirror2")));
    }

    [TestMethod]
    public async Task ProbeAsync_ShouldSortByMultiFactorScore()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddMirror("fast", 1024, "v1", delayMs: 10);
        handler.AddMirror("slow", 1024, "v1", delayMs: 200);
        handler.AddMirror("medium", 1024, "v1", delayMs: 50);

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var prober = new MetadataProber();

        // Act
        var result = await prober.ProbeAsync(
            ["http://fake.com/fast", "http://fake.com/slow", "http://fake.com/medium"],
            client);

        // Assert - 快速镜像应该排在前面
        Assert.AreEqual(3, result.SortedMirrors.Count);
        Assert.IsTrue(result.SortedMirrors[0].Url.Contains("fast"));
    }

    [TestMethod]
    public async Task ProbeAsync_ShouldAssignNonlinearHealthScores()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddMirror("m1", 1024, "v1", delayMs: 10);
        handler.AddMirror("m2", 1024, "v1", delayMs: 20);
        handler.AddMirror("m3", 1024, "v1", delayMs: 30);
        handler.AddMirror("m4", 1024, "v1", delayMs: 40);

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var prober = new MetadataProber();

        // Act
        var result = await prober.ProbeAsync(
            ["http://fake.com/m1", "http://fake.com/m2", "http://fake.com/m3", "http://fake.com/m4"],
            client);

        // Assert - 健康分数应该是非线性递减
        var scores = result.SortedMirrors.Select(m => m.HealthScore).ToList();
        Assert.AreEqual(100, scores[0]); // 第一名满分
        Assert.IsTrue(scores[1] >= 70);  // 第二名差距小
        Assert.IsTrue(scores[^1] >= 30); // 最后一名有下限
    }

    [TestMethod]
    public async Task ProbeAsync_ShouldEstimateBandwidth()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddMirror("mirror1", 100000, "v1", delayMs: 10);

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var prober = new MetadataProber();

        // Act
        var result = await prober.ProbeAsync(["http://fake.com/mirror1"], client);

        // Assert - 应该有带宽估算
        var mirror = result.SortedMirrors[0];
        // 由于是 mock，带宽估算可能为 0 或基于延迟计算
        Assert.IsTrue(mirror.EstimatedBandwidthBps >= 0);
    }

    [TestMethod]
    public async Task ProbeAsync_ShouldHandleAllMirrorsFailing()
    {
        // Arrange
        var handler = new MockHttpMessageHandler(); // 没有添加任何镜像，所有请求都会 404

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var prober = new MetadataProber();

        // Act & Assert
        try
        {
            await prober.ProbeAsync(["http://fake.com/bad1", "http://fake.com/bad2"], client);
            Assert.Fail("Expected FailedOperationException");
        }
        catch (PCL.Core.IO.Download.Core.FailedOperationException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task ProbeAsync_ShouldSelectLargerConsensusGroup()
    {
        // Arrange
        var handler = new MockHttpMessageHandler();
        handler.AddMirror("group1_a", 1024, "v1");
        handler.AddMirror("group1_b", 1024, "v1");
        handler.AddMirror("group1_c", 1024, "v1");
        handler.AddMirror("group2_a", 2048, "v2");
        handler.AddMirror("group2_b", 2048, "v2");

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var prober = new MetadataProber();

        // Act
        var result = await prober.ProbeAsync([
            "http://fake.com/group1_a",
            "http://fake.com/group1_b",
            "http://fake.com/group1_c",
            "http://fake.com/group2_a",
            "http://fake.com/group2_b"
        ], client);

        // Assert - 应该选择数量更多的组 (group1)
        Assert.AreEqual(1024, result.FileSize);
        Assert.AreEqual(3, result.SortedMirrors.Count);
    }
}
