using PCL.Core.IO.Download.Core;
using PCL.Core.IO.Download.IO;
using PCL.Core.IO.Download.Network;
using PCL.Core.IO.Download.Scheduling;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable FlagArgument

namespace PCL.Core.IO.Download;

/// <summary>
/// 下载器 - 支持多镜像智能选择、分块并发下载
/// </summary>
/// <param name="options">下载配置</param>
/// <param name="globalThrottle">全局并发限制</param>
/// <param name="client">HTTP客户端</param>
public class DownloadClient(DownloadOptions options, SemaphoreSlim globalThrottle, HttpClient client)
{
    #region Events

    public event DownloadStateChangeEventHandler? StateChanged;
    public event MirrorSwitchedEventHandler? MirrorSwitched;
    public event DownloadProgressEventHandler? ProgressChanged;

    #endregion

    private long _totalFileSize;
    private long _totalDownloadedBytes;
    private readonly object _progressLock = new();
    private volatile DownloadState _currentState = DownloadState.Preparing;
    private DateTime _lastProgressReport = DateTime.MinValue;
    private long _bytesSinceLastReport;
    private MirrorSelector? _mirrorSelector;
    private readonly CancellationTokenSource _globalTokenSource = new();
    private int _activeExecutingWorkers;

    /// <summary>
    /// 实际工作线程数
    /// </summary>
    public int ActuealWorkers { get; private set; }

    /// <summary>
    /// 开始下载
    /// </summary>
    public async Task StartAsync()
    {
        _ChangeState(DownloadState.Probing);

        var prober = new MetadataProber();
        var (fileSize, supportRange, mirrors) =
            await prober.ProbeAsync(options.MirrorUrls, client, _globalTokenSource.Token).ConfigureAwait(false);

        _totalFileSize = fileSize;
        _mirrorSelector = new MirrorSelector(mirrors);

        _ChangeState(DownloadState.Waiting);

        ActuealWorkers = (supportRange && fileSize > options.ChunkSizeBytes) ? options.MaxConcurrentWorkers : 1;

        _ChangeState(DownloadState.Downloading);

        var scheduler = new ChunkScheduler(_totalFileSize, options.ChunkSizeBytes);
        using var storage = new FileStorage(options.DestinationFilePath, _totalFileSize);

        var workerTasks = new Task[ActuealWorkers];
        for (var i = 0; i < ActuealWorkers; i++)
        {
            workerTasks[i] = _WorkerLoopAsync(scheduler, storage, _globalTokenSource.Token);
        }

        try
        {
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _ChangeState(DownloadState.Canceled);
            throw;
        }
        catch
        {
            _ChangeState(DownloadState.Failed);
            throw;
        }

        _ChangeState(DownloadState.Completed);
    }

    /// <summary>
    /// 取消下载
    /// </summary>
    public void Cancel()
    {
        if (!_globalTokenSource.IsCancellationRequested)
            _globalTokenSource.Cancel();
    }

    private async Task _WorkerLoopAsync(ChunkScheduler scheduler, FileStorage storage, CancellationToken globalToken)
    {
        while (!globalToken.IsCancellationRequested)
        {
            var chunkInfo = await scheduler.GetNextChunkAsync(globalToken).ConfigureAwait(false);
            if (chunkInfo is null) break;

            var chunk = chunkInfo.Value;
            var chunkCompleted = false;

            await globalThrottle.WaitAsync(globalToken).ConfigureAwait(false);

            var runningCount = Interlocked.Increment(ref _activeExecutingWorkers);
            if (runningCount == 1) _ChangeState(DownloadState.Downloading);

            while (!chunkCompleted && !globalToken.IsCancellationRequested)
            {
                var mirrorState = _mirrorSelector!.SelectBest();
                if (mirrorState is null)
                    throw new FailedOperationException("No available mirrors");

                var previousMirrorUrl = mirrorState.BaseInfo.Url;
                using var chunkCts = CancellationTokenSource.CreateLinkedTokenSource(globalToken);
                var bytesDownloaded = 0;
                var chunkSw = Stopwatch.StartNew();

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, mirrorState.BaseInfo.Url);
                    request.Headers.Range = new RangeHeaderValue(chunk.StartOffset, chunk.StartOffset + chunk.Length - 1);

                    using var response = await client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, globalToken)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(chunkCts.Token).ConfigureAwait(false);
                    using var bufferOwner = MemoryPool<byte>.Shared.Rent(options.MemoryBufferSizeBytes);
                    var buffer = bufferOwner.Memory;

                    await using var speedMonitor = new SpeedMonitor(
                        chunkCts,
                        options.MinSpeedThresholdBps,
                        options.SpeedCheckInterval);

                    int readBytes;
                    while ((readBytes = await stream.ReadAsync(buffer, chunkCts.Token).ConfigureAwait(false)) > 0)
                    {
                        await storage.WriteChunkAsync(
                            chunk.StartOffset + bytesDownloaded,
                            buffer[..readBytes],
                            globalToken).ConfigureAwait(false);

                        bytesDownloaded += readBytes;
                        speedMonitor.ReportBytesRead(readBytes);
                        Interlocked.Add(ref _totalDownloadedBytes, readBytes);
                        _ReportProgress(readBytes);
                    }

                    chunkSw.Stop();
                    var speedBps = chunkSw.ElapsedMilliseconds > 0
                        ? bytesDownloaded * 1000.0 / chunkSw.ElapsedMilliseconds
                        : bytesDownloaded;

                    mirrorState.ReportSuccess(speedBps, bytesDownloaded);
                    chunkCompleted = true;
                    scheduler.MarkChunkCompleted();
                }
                catch (OperationCanceledException) when (!globalToken.IsCancellationRequested)
                {
                    chunkSw.Stop();
                    mirrorState.ReportFailure(FailureType.SlowSpeed);
                    _NotifyMirrorSwitch(previousMirrorUrl, "Speed below threshold");
                    _ReturnChunk(scheduler, chunk, bytesDownloaded);
                    break;
                }
                catch (HttpRequestException)
                {
                    mirrorState.ReportFailure(FailureType.HttpError);
                    _NotifyMirrorSwitch(previousMirrorUrl, "HTTP error");
                    _ReturnChunk(scheduler, chunk, bytesDownloaded);
                    break;
                }
                catch (Exception)
                {
                    mirrorState.ReportFailure(FailureType.ConnectionError);
                    _NotifyMirrorSwitch(previousMirrorUrl, "Connection error");
                    _ReturnChunk(scheduler, chunk, bytesDownloaded);
                    break;
                }
                finally
                {
                    globalThrottle.Release();
                    var remaining = Interlocked.Decrement(ref _activeExecutingWorkers);
                    if (remaining == 0 && scheduler.HasPendingChunks)
                        _ChangeState(DownloadState.Waiting);
                }
            }
        }
    }

    private void _ReturnChunk(ChunkScheduler scheduler, ChunkInfo chunk, int bytesDownloaded)
    {
        if (bytesDownloaded < chunk.Length)
        {
            scheduler.ReturnIncompleteChunk(
                chunk.StartOffset + bytesDownloaded,
                chunk.Length - bytesDownloaded,
                chunk.ChunkIndex);
        }
    }

    private void _NotifyMirrorSwitch(string oldUrl, string reason)
    {
        MirrorSwitched?.Invoke(this, new MirrorSwitchedEventArgs
        {
            OldMirrorUrl = oldUrl,
            NewMirrorUrl = "(auto-select)",
            Reason = reason
        });
    }

    private void _ChangeState(DownloadState newState)
    {
        lock (_progressLock)
        {
            if (_currentState == newState)
            {
                return;
            }

            var oldState = _currentState;
            _currentState = newState;

            StateChanged?.Invoke(this, new DownloadStateChangeEventArgs { OldState = oldState, NewState = newState });
        }
    }

    private void _ReportProgress(long downloadedBytes)
    {
        lock (_progressLock)
        {
            _bytesSinceLastReport += downloadedBytes;

            var now = DateTime.UtcNow;
            var intervalSeconds = options.SpeedCheckInterval.TotalSeconds;

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