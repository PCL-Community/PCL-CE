using PCL.Core.IO.Download.Core;
using PCL.Core.IO.Download.IO;
using PCL.Core.IO.Download.Network;
using PCL.Core.IO.Download.Scheduling;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable FlagArgument

namespace PCL.Core.IO.Download;

// NOTE:
// Someone said I should reuse existing 'DownloadClient', but that is impossible
// This class cannot use cache (without HttpClient, because I've applied cache on it) (or 'reuseing')
// because 'DownloadClient' is only used for only one download job (like a file)
// If you want to decrease 'DownloadClient' entity creating, you should apply a cache system for it
// but which means a large refactoring and it is too hard to realize
public class DownloadClient
{
    #region Events

    public event DownloadStateChangeEventHandler? StateChanged;
    public event MirrorSwitchedEventHandler? MirrorSwitched;
    public event DownloadProgressEventHandler? ProgressChanged;

    #endregion

    private readonly HttpClient _httpClient;
    private readonly DownloadOptions _options;
    private long _totalFileSize;
    private long _totalDownloadedBytes;
    private readonly object _progressLock = new();
    private volatile DownloadState _currentState = DownloadState.Preparing;
    private DateTime _lastProgressReport = DateTime.MinValue;
    private long _bytesSinceLastReport;
    private List<MirrorInfo>? _mirrors;
    private readonly CancellationTokenSource _globalTokenSource = new();
    private int _activeExecutingWorkers;
    private readonly SemaphoreSlim _globalThrottle;

    public int ActiveWorkers { get; private set; }

    public DownloadClient(DownloadOptions options, SemaphoreSlim globalThrottle)
    {
        _options = options;
        _globalThrottle = globalThrottle;

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = options.MaxConcurrentWorkers * 2,
            ConnectTimeout = options.TimeOut
        };

        _httpClient = new HttpClient(handler) { Timeout = options.TimeOut };
    }

    public DownloadClient(DownloadOptions options, HttpClient client, SemaphoreSlim globalThrottle)
    {
        _options = options;
        _httpClient = client;
        _globalThrottle = globalThrottle;
    }

    public DownloadClient(DownloadOptions options, SocketsHttpHandler handler, SemaphoreSlim globalThrottle)
    {
        _options = options;
        _globalThrottle = globalThrottle;

        _httpClient = new HttpClient(handler) { Timeout = options.TimeOut };
    }


    public async Task StartAsync()
    {
        _ChangeState(DownloadState.Probing);

        var prober = new MetadataProber(_options.TimeOut);
        var (fileSize, supprotRange, mirrors) = await prober.ProbeAsync(_options.MirrorUrls, _httpClient).ConfigureAwait(false);
        _totalFileSize = fileSize;
        _mirrors = mirrors;

        _ChangeState(DownloadState.Waiting);

        var actualWorkers = (supprotRange && fileSize > _options.ChunkSizeBytes) ? _options.MaxConcurrentWorkers : 1;
        ActiveWorkers = actualWorkers;

        _ChangeState(DownloadState.Downloading);

        var scheduler = new ChunkScheduler(_totalFileSize, _options.ChunkSizeBytes);
        using var storage = new FileStorage(_options.DestinationFilePath, _totalFileSize);

        var workerTasks = new List<Task>();
        for (int i = 0; i < ActiveWorkers; i++)
        {
            workerTasks.Add(_WorkerLoopAsync(scheduler, storage, _mirrors!, _globalTokenSource.Token));
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

    public void Cancel()
    {
        if (_globalTokenSource.IsCancellationRequested)
        {
            return;
        }

        _globalTokenSource.Cancel();
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

            // global throttle
            // limited by <see cref="Config.Download.ThreadLimit">
            await _globalThrottle.WaitAsync(globalToken).ConfigureAwait(false);

            var currentRunning = Interlocked.Increment(ref _activeExecutingWorkers);
            if (currentRunning == 1) // only change state on first worker
            {
                _ChangeState(DownloadState.Downloading);
            }

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
                    // send request
                    var request = new HttpRequestMessage(HttpMethod.Get, currentMirror.Url);
                    request.Headers.Range =
                        new RangeHeaderValue(chunk.StartOffset, chunk.StartOffset + chunk.Length - 1);

                    using var response = await _httpClient
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, connectCts.Token)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    await using var stream =
                        await response.Content.ReadAsStreamAsync(streamReadCts.Token).ConfigureAwait(false);
                    using var bufferOwner = MemoryPool<byte>.Shared.Rent(_options.MemoryBufferSizeBytes);
                    var buffer = bufferOwner.Memory;

                    // perpar stream to write data
                    await using var speedMonitor = new SpeedMonitor(
                        streamReadCts,
                        _options.MinSpeedThresholdBps,
                        _options.SpeedCheckInterval);

                    // write data
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
                    // SppedMonitor is angry
                    // change mirror
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
                    // mirror dead
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
                finally
                {
                    _globalThrottle.Release();

                    // release resource and check state
                    var remainingRunning = Interlocked.Decrement(ref _activeExecutingWorkers);
                    if (remainingRunning == 0 && scheduler.HasPendingChunks)
                    {
                        _ChangeState(DownloadState.Waiting);
                    }
                }
            }
        }
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