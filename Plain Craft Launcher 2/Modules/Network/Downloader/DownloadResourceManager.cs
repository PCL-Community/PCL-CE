using System.Diagnostics;
using System.Collections.Concurrent;

namespace PCL.Network;

/// <summary>协调所有下载任务共享的连接数、缓冲区和限速额度。</summary>
internal static class DownloadResourceManager
{
    private static readonly AsyncQuota ConnectionQuota = new();
    private static readonly AsyncQuota BufferQuota = new();
    private static readonly ConcurrentDictionary<string, AsyncQuota> HostConnectionQuotas = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object BandwidthLock = new();
    private static long _nextBandwidthTick;

    public static async ValueTask<DownloadConnectionLease> AcquireConnectionAsync(string url,
        CancellationToken cancellationToken)
    {
        var host = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : url;
        var hostQuota = HostConnectionQuotas.GetOrAdd(host, static _ => new AsyncQuota());
        var hostLease = await hostQuota.AcquireAsync(1, () => ModNet.NetTaskConnectionsPerHostLimit, cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var globalLease = await ConnectionQuota.AcquireAsync(1,
                () => Math.Clamp(ModNet.NetTaskConnectionLimit, 1, ModNet.NetTaskConnectionLimitMax), cancellationToken)
                .ConfigureAwait(false);
            return new DownloadConnectionLease(globalLease, hostLease);
        }
        catch
        {
            hostLease.Dispose();
            throw;
        }
    }

    public static ValueTask<DownloadQuotaLease> ReserveBufferAsync(int bytes, CancellationToken cancellationToken)
    {
        return BufferQuota.AcquireAsync(bytes, () => ModNet.NetTaskBufferBudgetBytes, cancellationToken);
    }

    public static Task ThrottleAsync(int bytes, CancellationToken cancellationToken)
    {
        var limit = ModNet.NetTaskSpeedLimitHigh;
        if (limit <= 0 || bytes <= 0)
            return Task.CompletedTask;

        long delayTicks;
        lock (BandwidthLock)
        {
            var now = Stopwatch.GetTimestamp();
            var availableAt = Math.Max(now, _nextBandwidthTick);
            var duration = Math.Max(1L, (long)Math.Ceiling((double)bytes * Stopwatch.Frequency / limit));
            _nextBandwidthTick = availableAt + duration;
            delayTicks = availableAt - now;
        }

        return delayTicks <= 0
            ? Task.CompletedTask
            : Task.Delay(TimeSpan.FromSeconds((double)delayTicks / Stopwatch.Frequency), cancellationToken);
    }
}

internal sealed class DownloadConnectionLease(DownloadQuotaLease globalLease, DownloadQuotaLease hostLease) : IDisposable
{
    private DownloadQuotaLease? _globalLease = globalLease;
    private DownloadQuotaLease? _hostLease = hostLease;

    public void Dispose()
    {
        Interlocked.Exchange(ref _globalLease, null)?.Dispose();
        Interlocked.Exchange(ref _hostLease, null)?.Dispose();
    }
}

internal sealed class DownloadQuotaLease : IDisposable
{
    private AsyncQuota? _quota;
    private readonly long _amount;

    internal DownloadQuotaLease(AsyncQuota quota, long amount)
    {
        _quota = quota;
        _amount = amount;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _quota, null)?.Release(_amount);
    }
}

internal sealed class AsyncQuota
{
    private readonly object _lock = new();
    private readonly List<TaskCompletionSource> _waiters = new();
    private long _used;

    public async ValueTask<DownloadQuotaLease> AcquireAsync(long amount, Func<long> getCapacity,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        while (true)
        {
            TaskCompletionSource? waiter = null;
            lock (_lock)
            {
                var capacity = Math.Max(amount, getCapacity());
                if (_used + amount <= capacity)
                {
                    _used += amount;
                    return new DownloadQuotaLease(this, amount);
                }

                waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Add(waiter);
            }

            try
            {
                await waiter.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                lock (_lock)
                    _waiters.Remove(waiter);
                throw;
            }
        }
    }

    public void Release(long amount)
    {
        TaskCompletionSource? waiter = null;
        lock (_lock)
        {
            _used = Math.Max(0, _used - amount);
            if (_waiters.Count > 0)
            {
                waiter = _waiters[0];
                _waiters.RemoveAt(0);
            }
        }

        waiter?.TrySetResult();
    }
}
