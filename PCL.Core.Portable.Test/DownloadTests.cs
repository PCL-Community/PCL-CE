// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCL.Core.IO.Download;

namespace PCL.Core.Portable.Test;

[TestClass]
public sealed class DownloadTests
{
    [TestMethod]
    public async Task HttpConnectionReadsIntoCallerBuffer()
    {
        var expected = Encoding.UTF8.GetBytes("portable download payload");
        using var client = new HttpClient(new StaticResponseHandler(expected));
        await using var connection = new HttpDlConnection(client, "https://pcl.invalid/file");

        var info = await connection.StartAsync(0);
        var actual = new byte[expected.Length];
        var offset = 0;
        while (offset < actual.Length)
        {
            var read = await connection.ReadAsync(actual.AsMemory(offset));
            if (read == 0)
                break;
            offset += read;
        }

        Assert.AreEqual(expected.Length, info.Length);
        Assert.IsTrue(info.IsSupportSegment);
        Assert.AreEqual(expected.Length, offset);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task FileWriterCommitsAndCleansTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pcl-writer-{Guid.NewGuid():N}");
        var destination = Path.Combine(directory, "artifact.bin");
        var temporary = destination + ".PCLDownloading";
        var expected = Encoding.UTF8.GetBytes("atomic file writer");

        try
        {
            await using (var writer = new FileDlWriter(destination))
            {
                var stream = await writer.CreateStreamAsync();
                await stream.WriteAsync(expected);
                await writer.FinishAsync();
            }

            CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(destination));
            Assert.IsFalse(File.Exists(temporary));

            await using (var writer = new FileDlWriter(destination))
            {
                var stream = await writer.CreateStreamAsync();
                await stream.WriteAsync(expected);
                await writer.StopAsync();
            }

            Assert.IsFalse(File.Exists(temporary));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class StaticResponseHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentLength = content.Length;
            response.Headers.AcceptRanges.Add("bytes");
            return Task.FromResult(response);
        }
    }
}
