using PCL.Core.Net.Downloader.Core;
using PCL.Core.Net.Downloader.IO;
using PCL.Core.Net.Downloader.Network;
using PCL.Core.Net.Downloader.Scheduling;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Net.Downloader;

public class DownloadClient
{
    private readonly DownloadOptions _options;
    private readonly HttpClient _httpClient;

    #region Events

    public event DownloadStateChangeEventHandler? StateChanged;
    public event MirrorSwitchedEventHandler? MirrorSwitched;
    public event DownloadProgressEventHandler? ProgressChanged;

    #endregion

    private long _totalFileSize;
    private long _totalDownloadedBytes;
    private readonly object _progressLock = new();
    private DateTime _lastProgressReport = DateTime.MinValue;
    private long _bytesSinceLastReport;

    public DownloadClient(DownloadOptions options)
    {
        _options = options;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = options.MaxConcurrentWorkers * 2,
            ConnectTimeout = options.TimeOut
        };

        _httpClient = new HttpClient(handler) { Timeout = options.TimeOut };
    }

    public async Task StartAsync(CancellationToken globalToken = default)
    {
        _ChangeState(DownloadState.Preparing, DownloadState.Probing);

        var prober = new MetadataProber(_options.TimeOut);
        var (fileSize, supprotRange, mirrors) = await prober.ProbeAsync(_options.MirrorUrls, _httpClient).ConfigureAwait(false);
        _totalFileSize = fileSize;

        _ChangeState(DownloadState.Probing, DownloadState.Downloading);

        var actualWorkers = supprotRange && fileSize > _options.ChunkSizeBytes ? _options.MaxConcurrentWorkers : 1;

        var scheduler = new ChunkScheduler(fileSize, _options.ChunkSizeBytes);
        using var storage = new FileStorage(_options.DestinationFilePath, fileSize);

        var workerTasks = new List<Task>();
        for (int i = 0; i < actualWorkers; i++)
        {
            workerTasks.Add(_WorkerLoopAsync(scheduler, storage, mirrors, globalToken));
        }

        await Task.WhenAll(workerTasks).ConfigureAwait(false);

        _ChangeState(DownloadState.Downloading, DownloadState.Completed);
    }

    private async Task _WorkerLoopAsync(ChunkScheduler scheduler,
        FileStorage storage,
        List<MirrorInfo> mirrors,
        CancellationToken globalToken)
    {
        while (!globalToken.IsCancellationRequested)
        {
            // get chunk
            var chunkInfo = await scheduler.GetNextChunkAsync(globalToken).ConfigureAwait(false);
            if (chunkInfo is null)
            {
                break;
            }

            var chunk = (ChunkInfo)chunkInfo;
            var chunkSuccessFully = false;

            while (!(chunkSuccessFully || globalToken.IsCancellationRequested))
            {
                var currentMirror = mirrors.OrderByDescending(m => m.HealthScore).FirstOrDefault(m => m.IsAlive);

                if (currentMirror is null)
                {
                    throw new FailedOperationException("All mirros have been un-usable");
                }

                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
                connectCts.CancelAfter(_options.TimeOut);

                using var streamReadCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
                var bytesDownloadedInThisChunk = 0;

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, currentMirror.Url);
                    request.Headers.Range =
                        new RangeHeaderValue(chunk.StartOffset, chunk.StartOffset + chunk.Length - 1);

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectCts.Token).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(streamReadCts.Token).ConfigureAwait(false);
                    using var bufferOwner = MemoryPool<byte>.Shared.Rent(81920); // 80KB
                    var buffer = bufferOwner.Memory;

                    await using var speedMonitor = new SpeedMonitor(
                        streamReadCts,
                        _options.MinSpeedThresholdBps,
                        _options.SpeedCheckInterval);

                    int readBytes;
                    while ((readBytes = await stream.ReadAsync(buffer, connectCts.Token).ConfigureAwait(false)) > 0)
                    {
                        await storage.WriteChunkAsync(
                            chunk.StartOffset + bytesDownloadedInThisChunk,
                            buffer.Slice(0, readBytes),
                            globalToken).ConfigureAwait(false);

                        bytesDownloadedInThisChunk += readBytes;
                        Interlocked.Add(ref _totalDownloadedBytes, readBytes);

                        _ReportProgress(bytesDownloadedInThisChunk);
                    }

                    chunkSuccessFully = true;
                    scheduler.MarkChunkCompleted();
                }
                catch (OperationCanceledException) when (!globalToken.IsCancellationRequested)
                {
                    var reason = connectCts.IsCancellationRequested ? "Timeout" : "Speed too low";

                    currentMirror.HealthScore -= 20;
                    MirrorSwitched?.Invoke(this, new MirrorSwitchedEventArgs
                    {
                        OldMirrorUrl = currentMirror.Url,
                        NewMirrorUrl = "Will be selected in next interaction",
                        Reason = reason
                    });

                    scheduler.ReturnIncompleteChunk(
                        chunk.StartOffset + bytesDownloadedInThisChunk,
                        chunk.Length - bytesDownloadedInThisChunk,
                        chunk.ChunkIndex);

                    break;
                }
                catch
                {
                    currentMirror.HealthScore -= 50;
                    if (currentMirror.HealthScore < 0)
                    {
                        currentMirror.IsAlive = false;
                    }

                    scheduler.ReturnIncompleteChunk(
                        chunk.StartOffset + bytesDownloadedInThisChunk,
                        chunk.Length - bytesDownloadedInThisChunk,
                        chunk.ChunkIndex);
                    break;
                }
            }
        }
    }

    private void _ChangeState(DownloadState oldState, DownloadState newState)
    {
        StateChanged?.Invoke(this, new DownloadStateChangeEventArgs { OldState = oldState, NewState = newState });
    }

    private void _ReportProgress(long downloadedBytes)
    {
        lock (_progressLock)
        {
            _bytesSinceLastReport += downloadedBytes;

            var now = DateTime.UtcNow;
            var intervalSeconds = _options.SpeedCheckInterval.TotalSeconds;

            if (_lastProgressReport == DateTime.MinValue ||
                (now - _lastProgressReport).TotalSeconds >= intervalSeconds)
            {
                var denom = Math.Max(1e-6, intervalSeconds);
                var speed = _bytesSinceLastReport / denom;

                ProgressChanged?.Invoke(this, new DownloadProgressEventArgs
                {
                    TotalBytes = _totalFileSize,
                    DownloadedBytes = _totalDownloadedBytes,
                    CurrentSpeedBytesPerSecond = speed
                });

                _bytesSinceLastReport = 0;
                _lastProgressReport = now;
            }
        }
    }
}