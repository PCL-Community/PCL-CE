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
    public async Task HttpConnectionRejectsMismatchedPartialContentRange()
    {
        var expected = Encoding.UTF8.GetBytes("portable range payload");
        using var client = new HttpClient(new MismatchedRangeResponseHandler(expected));
        await using var connection = new HttpDlConnection(client, "https://pcl.invalid/file");

        await Assert.ThrowsExactlyAsync<IOException>(
            () => connection.StartAsync(9).AsTask());
    }

    [TestMethod]
    public async Task FileWriterPreservesPartialTemporaryFileUntilCommit()
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
                Assert.AreEqual(expected.Length, writer.ExistingLength);
            }

            Assert.IsTrue(File.Exists(temporary));
            CollectionAssert.AreEqual(expected, await File.ReadAllBytesAsync(temporary));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadServiceResumesPartialTemporaryFile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"pcl-resume-download-{Guid.NewGuid():N}");
        var destination = Path.Combine(directory, "artifact.bin");
        var temporary = destination + ".PCLDownloading";
        var expected = Encoding.UTF8.GetBytes("portable resumable download payload");
        var partialLength = 9;
        var handler = new RangeResponseHandler(expected);
        using var client = new HttpClient(handler);
        var progress = new List<DownloadProgress>();

        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(temporary, expected[..partialLength]);

            var result = await new DownloadService().DownloadAsync(
                new DownloadRequest
                {
                    Sources = ["https://pcl.invalid/resume"],
                    DestinationPath = destination,
                    ConnectionFactory = url =>
                        new HttpDlConnection(client, url)
                },
                progress.Add);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(partialLength, handler.RequestedOffset);
            Assert.AreEqual(expected.Length, result.TotalBytes);
            Assert.IsFalse(File.Exists(temporary));
            CollectionAssert.AreEqual(
                expected,
                await File.ReadAllBytesAsync(destination));
            Assert.IsTrue(progress.Any(item =>
                item.Stage == DownloadStage.Reading &&
                item.DownloadedBytes == partialLength));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task DownloadServiceFallsBackAndCommitsSuccessfulSource()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"pcl-download-{Guid.NewGuid():N}");
        var destination = Path.Combine(directory, "artifact.bin");
        var expected = Encoding.UTF8.GetBytes("portable failover payload");
        using var client = new HttpClient(new RouteResponseHandler(expected));
        var stages = new List<DownloadStage>();

        try
        {
            var result = await new DownloadService().DownloadAsync(
                new DownloadRequest
                {
                    Sources =
                    [
                        "https://pcl.invalid/fail",
                        "https://pcl.invalid/success"
                    ],
                    DestinationPath = destination,
                    ConnectionFactory = url =>
                        new HttpDlConnection(client, url)
                },
                progress => stages.Add(progress.Stage));

            Assert.IsTrue(result.Success);
            Assert.AreEqual("https://pcl.invalid/success", result.SuccessfulSource);
            Assert.AreEqual(1, result.Errors.Count);
            CollectionAssert.AreEqual(
                expected,
                await File.ReadAllBytesAsync(destination));
            CollectionAssert.Contains(stages, DownloadStage.Retrying);
            CollectionAssert.Contains(stages, DownloadStage.Completed);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SharedDownloadKeepsRunningWhenOneWaiterCancels()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"pcl-shared-download-{Guid.NewGuid():N}");
        var destination = Path.Combine(directory, "artifact.bin");
        var expected = Encoding.UTF8.GetBytes("shared operation");
        var gate = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var starts = 0;
        var service = new DownloadService();
        var request = new DownloadRequest
        {
            Sources = ["https://pcl.invalid/shared"],
            DestinationPath = destination,
            ConnectionFactory = _ =>
            {
                Interlocked.Increment(ref starts);
                return new GatedConnection(expected, gate.Task);
            }
        };
        using var cancellation = new CancellationTokenSource();

        try
        {
            var canceledWaiter = service.DownloadAsync(
                request,
                cancellationToken: cancellation.Token);
            var successfulWaiter = service.DownloadAsync(request);
            cancellation.Cancel();
            await Assert.ThrowsExactlyAsync<TaskCanceledException>(
                () => canceledWaiter);

            gate.SetResult();
            var result = await successfulWaiter;

            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, starts);
            CollectionAssert.AreEqual(
                expected,
                await File.ReadAllBytesAsync(destination));
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

    private sealed class RouteResponseHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/fail")
                return Task.FromResult(new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable));

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentLength = content.Length;
            return Task.FromResult(response);
        }
    }

    private sealed class RangeResponseHandler(byte[] content) : HttpMessageHandler
    {
        public long RequestedOffset { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestedOffset = request.Headers.Range?.Ranges.FirstOrDefault()?.From ?? 0;
            byte[] body = content.AsSpan((int)RequestedOffset).ToArray();
            var response = new HttpResponseMessage(
                RequestedOffset > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(body)
            };
            response.Content.Headers.ContentLength = body.Length;
            response.Headers.AcceptRanges.Add("bytes");
            if (RequestedOffset > 0)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                    RequestedOffset,
                    content.Length - 1,
                    content.Length);
            }

            return Task.FromResult(response);
        }
    }

    private sealed class MismatchedRangeResponseHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(content)
            };
            response.Content.Headers.ContentLength = content.Length;
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(
                0,
                content.Length - 1,
                content.Length);
            response.Headers.AcceptRanges.Add("bytes");
            return Task.FromResult(response);
        }
    }

    private sealed class GatedConnection(
        byte[] content,
        Task gate) : IDlConnection
    {
        private bool _read;

        public ValueTask<NDlConnectionInfo> StartAsync(
            long beginOffset,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new NDlConnectionInfo(
                content.Length,
                beginOffset,
                content.Length - 1,
                false));

        public ValueTask StopAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_read)
                return 0;
            await gate.WaitAsync(cancellationToken);
            content.CopyTo(buffer);
            _read = true;
            return content.Length;
        }
    }
}
