using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Storage.Cache.Model;
using PCL.Core.Logging;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Storage.Cache;

internal class CacheEvictionService(SqliteCacheStorage db, FileCacheStorage files, CacheOptions options)
{
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private readonly object _startLock = new();

    public void Start()
    {
        lock (_startLock)
        {
            if (_loop is not null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            var cancellationToken = _cts.Token;
            _loop = Task.Run(() => _EvictionLoopAsync(cancellationToken));
        }
    }

    public async ValueTask StopAsync()
    {
        CancellationTokenSource? cancellation;
        Task? loop;
        lock (_startLock)
        {
            cancellation = _cts;
            loop = _loop;
            _cts = null;
            _loop = null;
        }

        if (cancellation is null)
            return;

        cancellation.Cancel();
        try
        {
            if (loop is not null)
                await loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    public void CheckThreshold()
    {
        // NOTE: this method will not be implemented
        // because current eviction strategy is enough to keep the cache size under control without checking threshold after each write operation.
    }

    private async Task _EvictionLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(options.EvictionInterval, ct).ConfigureAwait(false);
                var now = DateTime.UtcNow;
                var expiredFileHashes = await db
                    .GetExpiredFileHashesAsync(now, ct)
                    .ConfigureAwait(false);
                await db.DeleteExpiredAsync(now, ct).ConfigureAwait(false);
                foreach (var hash in expiredFileHashes)
                    await files.ReleaseAsync(hash).ConfigureAwait(false);

                var stats = await db.GetStatsAsync(ct).ConfigureAwait(false);
                var excess = stats.TotalSizeBytes - (options.MaxCacheSize - options.ReserveBytes);
                if (excess > 0)
                {
                    await _EvictAsync(excess, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "CacheEviction", $"An error occurred while evicting cache entries:");
            }
        }
    }

    private async Task _EvictAsync(long targetBytes, CancellationToken ct)
    {
        var freed = 0L;
        const int batchSize = 50;

        while (freed < targetBytes && !ct.IsCancellationRequested)
        {
            var candidates = await db.GetEvictionCandidatesAsync(batchSize, ct).ConfigureAwait(false);
            if (candidates.Count == 0)
            {
                return;
            }

            foreach (var can in candidates)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                await db.DeleteAsync(can.CacheKey!, ct).ConfigureAwait(false);

                if (can.EntryType is EntryType.FileRef && can.FileHash is not null)
                {
                    await files.ReleaseAsync(can.FileHash).ConfigureAwait(false);
                }

                freed += can.DataSize;
            }
        }
    }
}

public record CacheEntryEvictedEventArgs(string Key, long DataSize, int Priority, long HitCount);
