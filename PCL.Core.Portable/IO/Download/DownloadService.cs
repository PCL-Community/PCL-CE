// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PCL.Core.IO.Download;

/// <summary>
/// Coordinates failover downloads while sharing one transfer per destination.
/// Each caller retains an independent cancellation token and progress callback.
/// </summary>
public sealed class DownloadService
{
    private const int DefaultBufferSize = 128 * 1024;

    private readonly ConcurrentDictionary<string, Lazy<DownloadOperation>> _active =
        new(GetPathComparer());
    private readonly int _bufferSize;

    public DownloadService(int bufferSize = DefaultBufferSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);
        _bufferSize = bufferSize;
    }

    public Task<DownloadTransferResult> DownloadAsync(
        DownloadRequest request,
        Action<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var destinationPath = Path.GetFullPath(request.DestinationPath);
        var lazyOperation = _active.GetOrAdd(
            destinationPath,
            path => new Lazy<DownloadOperation>(
                () => new DownloadOperation(
                    operation => ExecuteAndCompleteAsync(
                        request with { DestinationPath = path },
                        path,
                        operation)),
                LazyThreadSafetyMode.ExecutionAndPublication));
        var operation = lazyOperation.Value;
        return operation.WaitAsync(progress, cancellationToken);
    }

    private async Task ExecuteAndCompleteAsync(
        DownloadRequest request,
        string destinationPath,
        DownloadOperation operation)
    {
        try
        {
            operation.SetResult(await DownloadCoreAsync(
                    request,
                    operation.Report,
                    operation.CancellationToken)
                .ConfigureAwait(false));
        }
        catch (OperationCanceledException exception)
        {
            operation.SetCanceled(exception.CancellationToken);
        }
        catch (Exception exception)
        {
            operation.SetException(exception);
        }
        finally
        {
            if (_active.TryGetValue(destinationPath, out var lazyOperation) &&
                lazyOperation.IsValueCreated &&
                ReferenceEquals(lazyOperation.Value, operation))
                _active.TryRemove(
                    new KeyValuePair<string, Lazy<DownloadOperation>>(
                        destinationPath,
                        lazyOperation));
            operation.Dispose();
        }
    }

    private async Task<DownloadTransferResult> DownloadCoreAsync(
        DownloadRequest request,
        Action<DownloadProgress> report,
        CancellationToken cancellationToken)
    {
        var errors = new List<DownloadAttemptError>();
        var startedAt = Stopwatch.GetTimestamp();

        foreach (var source in request.Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(source))
                continue;

            IDlConnection? connection = null;
            IDlWriter? writer = null;
            try
            {
                report(new DownloadProgress(
                    DownloadStage.Connecting,
                    source,
                    0,
                    -1,
                    0));
                connection = request.ConnectionFactory(source)
                             ?? throw new InvalidOperationException(
                                 $"No download connection was created for {source}.");
                var connectionInfo = await connection
                    .StartAsync(0, cancellationToken)
                    .ConfigureAwait(false);

                writer = request.WriterFactory(request.DestinationPath)
                         ?? throw new InvalidOperationException(
                             $"No download writer was created for {request.DestinationPath}.");
                var writeStream = await writer
                    .CreateStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                report(new DownloadProgress(
                    DownloadStage.Reading,
                    source,
                    0,
                    connectionInfo.Length,
                    0));

                var buffer = ArrayPool<byte>.Shared.Rent(_bufferSize);
                try
                {
                    var readStartedAt = Stopwatch.GetTimestamp();
                    long totalRead = 0;
                    while (true)
                    {
                        var read = await connection
                            .ReadAsync(
                                buffer.AsMemory(0, _bufferSize),
                                cancellationToken)
                            .ConfigureAwait(false);
                        if (read == 0)
                            break;

                        await writeStream
                            .WriteAsync(
                                buffer.AsMemory(0, read),
                                cancellationToken)
                            .ConfigureAwait(false);
                        totalRead += read;
                        report(new DownloadProgress(
                            DownloadStage.Downloading,
                            source,
                            totalRead,
                            Math.Max(connectionInfo.Length, totalRead),
                            CalculateSpeed(totalRead, readStartedAt)));
                    }

                    await writeStream
                        .FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
                    report(new DownloadProgress(
                        DownloadStage.Committing,
                        source,
                        totalRead,
                        totalRead,
                        0));
                    await writer
                        .FinishAsync(cancellationToken)
                        .ConfigureAwait(false);
                    report(new DownloadProgress(
                        DownloadStage.Completed,
                        source,
                        totalRead,
                        totalRead,
                        0));
                    return new DownloadTransferResult(
                        true,
                        request.DestinationPath,
                        source,
                        totalRead,
                        Stopwatch.GetElapsedTime(startedAt),
                        errors.ToArray());
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add(new DownloadAttemptError(
                    source,
                    exception.Message,
                    exception));
                report(new DownloadProgress(
                    DownloadStage.Retrying,
                    source,
                    0,
                    -1,
                    0));
            }
            finally
            {
                await StopWriterAsync(writer).ConfigureAwait(false);
                await StopConnectionAsync(connection).ConfigureAwait(false);
            }
        }

        report(new DownloadProgress(
            DownloadStage.Failed,
            string.Empty,
            0,
            -1,
            0));
        return new DownloadTransferResult(
            false,
            request.DestinationPath,
            null,
            0,
            Stopwatch.GetElapsedTime(startedAt),
            errors.ToArray());
    }

    private static long CalculateSpeed(long bytes, long startedAt)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt).TotalSeconds;
        return elapsed < 0.1 ? 0 : checked((long)(bytes / elapsed));
    }

    private static async ValueTask StopWriterAsync(IDlWriter? writer)
    {
        if (writer is null)
            return;
        try
        {
            await writer.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Cleanup must not hide the original transfer outcome.
        }
    }

    private static async ValueTask StopConnectionAsync(IDlConnection? connection)
    {
        if (connection is null)
            return;
        try
        {
            await connection.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Cleanup must not hide the original transfer outcome.
        }
    }

    private static void ValidateRequest(DownloadRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationPath);
        ArgumentNullException.ThrowIfNull(request.Sources);
        ArgumentNullException.ThrowIfNull(request.ConnectionFactory);
        ArgumentNullException.ThrowIfNull(request.WriterFactory);
        if (request.Sources.Count == 0)
            throw new ArgumentException(
                "At least one download source is required.",
                nameof(request));
    }

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private sealed class DownloadOperation(
        Func<DownloadOperation, Task> start) : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly TaskCompletionSource<DownloadTransferResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<long, Action<DownloadProgress>> _subscribers = [];
        private readonly object _sync = new();
        private long _subscriberId;
        private int _waiterCount;
        private int _started;
        private bool _disposed;

        public CancellationToken CancellationToken => _cancellation.Token;

        public async Task<DownloadTransferResult> WaitAsync(
            Action<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            var subscriberId = progress is null ? 0 : AddSubscriber(progress);
            Interlocked.Increment(ref _waiterCount);
            try
            {
                if (Interlocked.CompareExchange(ref _started, 1, 0) == 0)
                    _ = start(this);
                return await _completion.Task
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                if (subscriberId != 0)
                    RemoveSubscriber(subscriberId);
                if (Interlocked.Decrement(ref _waiterCount) == 0 &&
                    !_completion.Task.IsCompleted)
                    await _cancellation.CancelAsync().ConfigureAwait(false);
            }
        }

        public void Report(DownloadProgress progress)
        {
            lock (_sync)
            {
                foreach (var subscriber in _subscribers.Values)
                {
                    try
                    {
                        subscriber(progress);
                    }
                    catch
                    {
                        // UI progress handlers must not terminate the transfer.
                    }
                }
            }
        }

        public void SetResult(DownloadTransferResult result) =>
            _completion.TrySetResult(result);

        public void SetCanceled(CancellationToken cancellationToken) =>
            _completion.TrySetCanceled(cancellationToken);

        public void SetException(Exception exception) =>
            _completion.TrySetException(exception);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _cancellation.Dispose();
        }

        private long AddSubscriber(Action<DownloadProgress> progress)
        {
            var id = Interlocked.Increment(ref _subscriberId);
            lock (_sync)
                _subscribers.Add(id, progress);
            return id;
        }

        private void RemoveSubscriber(long subscriberId)
        {
            lock (_sync)
                _subscribers.Remove(subscriberId);
        }
    }
}
