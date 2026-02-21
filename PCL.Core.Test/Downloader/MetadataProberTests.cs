using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Download.Network;

namespace PCL.Core.Test.Downloader;

[TestClass]
public class MetadataProberTests
{
    // 一个简单的 Mock Http 处理器，用于模拟服务器返回
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            if (request.RequestUri.AbsoluteUri.Contains("mirror1"))
            {
                response.Content = new StringContent("");
                response.Content.Headers.ContentLength = 1024;
                response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                response.Headers.AcceptRanges.Add("bytes");
            }
            else if (request.RequestUri.AbsoluteUri.Contains("mirror2"))
            {
                response.Content = new StringContent("");
                response.Content.Headers.ContentLength = 1024;
                response.Headers.ETag = new EntityTagHeaderValue("\"v1\""); // 与 mirror1 一致
                response.Headers.AcceptRanges.Add("bytes");
            }
            else if (request.RequestUri.AbsoluteUri.Contains("mirror3_bad"))
            {
                response.Content = new StringContent("");
                response.Content.Headers.ContentLength = 999; // 大小不一致
                response.Headers.ETag = new EntityTagHeaderValue("\"v0_old\""); // ETag不一致
            }

            return Task.FromResult(response);
        }
    }

    [TestMethod]
    public async Task ProbeAsync_ShouldFilterOutInconsistentMirrors()
    {
        // Arrange
        var mockHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(mockHandler);
        var prober = new MetadataProber(TimeSpan.FromSeconds(5));

        var urls = new List<string>
        {
            "http://fake.com/mirror1",
            "http://fake.com/mirror2",
            "http://fake.com/mirror3_bad"
        };

        // Act
        var result = await prober.ProbeAsync(urls, httpClient);

        // Assert
        Assert.AreEqual(1024, result.FileSize, "应选取共识组的文件大小");
        Assert.IsTrue(result.SupportRange, "应正确识别 Accept-Ranges");

        // 核心验证：剔除错误镜像
        Assert.HasCount(2, result.SortedMirrors, "应该剔除了 mirror3_bad");
        Assert.IsTrue(result.SortedMirrors.Exists(m => m.Url.Contains("mirror1")));
        Assert.IsTrue(result.SortedMirrors.Exists(m => m.Url.Contains("mirror2")));
    }
}