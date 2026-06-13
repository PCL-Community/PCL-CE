using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PCL.Core.App;
using PCL.Core.IO.Storage.Cache.Model;

// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.IO.Storage.Cache;


/// <summary>
/// A cache service that provides asynchronous methods to store and retrieve data with support for expiration, tagging, grouping, and priority.<br/>
/// It uses a combination of SQLite for metadata storage and the file system for large data storage.<br/>
/// The service also includes an eviction mechanism to manage cache size and expired entries.<br/>
/// </summary>
public class CacheService : ICacheService, IAsyncDisposable
{
    private readonly CacheOptions _options;
    private readonly SchemaManager _schemaManager;
    private readonly SqliteCacheStorage _db;
    private readonly FileCacheStorage _files;
    private readonly CacheEvictionService _eviction;

    private long _hits;
    private long _misses;

    private bool _disposed;

    private static readonly JsonSerializerOptions _JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };


    public CacheService()
    {
        _options = new CacheOptions
        {
            DatabasePath = Path.Combine(Paths.Temp, "Cache", "pcl_ce_cache.db"),
            FileCacheRoot = Path.Combine(Paths.Temp, "Cache", "files")
        };

        _schemaManager = new SchemaManager($"Data Source={_options.DatabasePath}");
        _db = new SqliteCacheStorage(_options.DatabasePath);
        _files = new FileCacheStorage(_options.FileCacheRoot, _options.EnableCompression);
        _eviction = new CacheEvictionService(_db, _files, _options);
    }

    /// <summary>
    /// Unit Test constructor. This constructor allows injecting custom options for testing purposes.
    /// </summary>
    internal CacheService(CacheOptions options)
    {
        _options = options;
        _schemaManager = new SchemaManager($"Data Source={_options.DatabasePath}");
        _db = new SqliteCacheStorage(_options.DatabasePath);
        _files = new FileCacheStorage(_options.FileCacheRoot, _options.EnableCompression);
        _eviction = new CacheEvictionService(_db, _files, _options);
    }

    internal async Task InitializeAsync()
    {
        Directory.CreateDirectory(_options.FileCacheRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(_options.DatabasePath)!);


        await _schemaManager.EnsureCurrentSchemaAsync().ConfigureAwait(false);

        var persistedFileReferences = await _db
            .GetAllFileHashesAsync(CancellationToken.None)
            .ConfigureAwait(false);
        _files.RestoreReferences(persistedFileReferences);

        var startupTime = DateTime.UtcNow;
        var expiredFileReferences = await _db
            .GetExpiredFileHashesAsync(startupTime, CancellationToken.None)
            .ConfigureAwait(false);
        await _db.CleanupStartupAsync(startupTime).ConfigureAwait(false);
        await _ReleaseFilesAsync(expiredFileReferences).ConfigureAwait(false);

        _eviction.Start();

    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(string key, T value, CachePolicy? policy = null,
            CancellationToken ct = default)
    {
        _ThrowIfNotReady();
        policy ??= CachePolicy.Default;
        var previousEntry = await _db.LookupAsync(key, ct).ConfigureAwait(false);

        var bytes = _Serialize(value);
        var useInlineStorage = policy.StorageMode switch
        {
            CacheStorageMode.Inline => true,
            CacheStorageMode.FileMapped => false,
            _ => bytes.Length <= _options.MaxInlineSize
        };

        var entry = new CacheEntry
        {
            CacheKey = key,
            ContentType = typeof(T).Name,
            ContentVersion = policy.ContentVersion,
            DataSize = bytes.Length,
            Tags = policy.Tags ?? string.Empty,
            GroupName = policy.Group ?? string.Empty,
            Priority = (int)policy.Priority,
            ExpiresAt = _ComputeExpiry(policy),
        };

        if (useInlineStorage)
        {
            entry = entry with
            {
                EntryType = EntryType.Inline,
                Data = bytes,
                ContentHash = _ComputeSha256(bytes)
            };
        }
        else
        {
            using var ms = new MemoryStream(bytes);
            var fileHash = await _files.StoreAsync(ms, cancellationToken: ct).ConfigureAwait(false);
            entry = entry with
            {
                EntryType = EntryType.FileRef,
                FileHash = fileHash,
                ContentHash = fileHash
            };
        }

        try
        {
            await _db.UpsertAsync(entry, ct).ConfigureAwait(false);
        }
        catch
        {
            if (entry.FileHash is not null)
                await _files.ReleaseAsync(entry.FileHash).ConfigureAwait(false);
            throw;
        }

        if (previousEntry?.FileHash is not null)
            await _files.ReleaseAsync(previousEntry.FileHash).ConfigureAwait(false);

        _eviction.CheckThreshold();
    }

    /// <inheritdoc/>
    public async Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken ct = default)
    {
        _ThrowIfNotReady();

        var entry = await _db.LookupAsync(key, ct).ConfigureAwait(false);
        if (entry is null) { Interlocked.Increment(ref _misses); return CacheResult<T>.Miss; }

        // 检查过期
        if (entry.ExpiresAt is not null && (DateTime)entry.ExpiresAt < DateTime.UtcNow)
        {
            await _db.DeleteAsync(key, ct).ConfigureAwait(false);
            if (entry.FileHash is not null)
            {
                await _files.ReleaseAsync(entry.FileHash).ConfigureAwait(false);
            }
            Interlocked.Increment(ref _misses);
            return CacheResult<T>.Miss;
        }

        Interlocked.Increment(ref _hits);
        byte[] data;

        // 读取数据
        if (entry is { EntryType: EntryType.FileRef, FileHash: not null })
        {
            await using var stream = _files.Retrieve(entry.FileHash);
            if (stream is null)
            {
                await _db.DeleteAsync(key, ct).ConfigureAwait(false);
                Interlocked.Increment(ref _misses);
                return CacheResult<T>.Miss;
            }

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            data = ms.ToArray();
        }
        else
        {
            data = entry.Data ?? [];
        }

        // 反序列化
        var value = _Deserialize<T>(data);

        // 更新访问时间
        await _db.TouchAsync(key, ct).ConfigureAwait(false);

        return CacheResult<T>.Hit(value, entry.CachedAt);
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string key)
    {
        _ThrowIfNotReady();
        var entry = await _db.LookupAsync(key, CancellationToken.None).ConfigureAwait(false);
        if (entry is null) return false;
        return entry.ExpiresAt is null || (DateTime)entry.ExpiresAt >= DateTime.UtcNow;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(string key)
    {
        _ThrowIfNotReady();
        var entry = await _db.LookupAsync(key, CancellationToken.None).ConfigureAwait(false);

        if (entry is null)
        {
            return false;
        }

        var deleted = await _db.DeleteAsync(key, CancellationToken.None).ConfigureAwait(false);
        if (deleted && entry.FileHash is not null)
            await _files.ReleaseAsync(entry.FileHash).ConfigureAwait(false);
        return deleted;
    }


    /// <inheritdoc/>
    public async Task<string?> GetCachedFilePathAsync(string key)
    {
        var entry = await _db.LookupAsync(key, CancellationToken.None).ConfigureAwait(false);

        if (entry?.FileHash is null)
        {
            return null;
        }

        return _files.GetFilePath(entry.FileHash);
    }

    /// <inheritdoc/>
    public async Task<string> CacheFileAsync(string key, Stream source,
        CachePolicy? policy = null, CancellationToken ct = default)
    {
        _ThrowIfNotReady();
        policy ??= CachePolicy.Default;

        var previousEntry = await _db.LookupAsync(key, ct).ConfigureAwait(false);
        CountingReadStream? countingSource = null;
        var storageSource = source;
        var dataSize = source.CanSeek ? source.Length : 0;
        if (!source.CanSeek)
        {
            countingSource = new CountingReadStream(source);
            storageSource = countingSource;
        }

        var hash = await _files
            .StoreAsync(storageSource, cancellationToken: ct)
            .ConfigureAwait(false);
        if (countingSource is not null)
            dataSize = countingSource.BytesRead;

        var entry = new CacheEntry
        {
            CacheKey = key,
            EntryType = EntryType.FileRef,
            ContentType = "application/octet-stream",
            DataSize = dataSize,
            FileHash = hash,
            ContentHash = hash,
            Tags = policy.Tags ?? "",
            GroupName = policy.Group ?? "",
            Priority = (int)policy.Priority,
            ExpiresAt = _ComputeExpiry(policy),
        };
        try
        {
            await _db.UpsertAsync(entry, ct).ConfigureAwait(false);
        }
        catch
        {
            await _files.ReleaseAsync(hash).ConfigureAwait(false);
            throw;
        }

        if (previousEntry?.FileHash is not null)
            await _files.ReleaseAsync(previousEntry.FileHash).ConfigureAwait(false);
        _eviction.CheckThreshold();
        return hash;
    }


    /// <inheritdoc/>
    public async Task<int> DeleteByGroupAsync(string groupName)
    {
        _ThrowIfNotReady();

        var fileHashes = await _db.GetFileHashesByGroupAsync(groupName, CancellationToken.None).ConfigureAwait(false);
        var deleted = await _db.DeleteByGroupAsync(groupName, CancellationToken.None).ConfigureAwait(false);
        await _ReleaseFilesAsync(fileHashes).ConfigureAwait(false);
        return deleted;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteByTagAsync(string tag)
    {
        _ThrowIfNotReady();
        var fileHashes = await _db.GetFileHashesByTagAsync(tag, CancellationToken.None).ConfigureAwait(false);
        var deleted = await _db.DeleteByTagAsync(tag, CancellationToken.None).ConfigureAwait(false);
        await _ReleaseFilesAsync(fileHashes).ConfigureAwait(false);
        return deleted;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteExpiredAsync()
    {
        _ThrowIfNotReady();
        var now = DateTime.UtcNow;
        var fileHashes = await _db
            .GetExpiredFileHashesAsync(now, CancellationToken.None)
            .ConfigureAwait(false);
        var count = await _db.DeleteExpiredAsync(now, CancellationToken.None).ConfigureAwait(false);
        await _ReleaseFilesAsync(fileHashes).ConfigureAwait(false);
        _eviction.CheckThreshold();
        return count;
    }


    /// <inheritdoc/>
    public async Task<CacheStats> GetStatsAsync()
    {
        var row = await _db.GetStatsAsync(CancellationToken.None).ConfigureAwait(false);
        return new CacheStats
        {
            TotalEntries = row.TotalEntries,
            TotalSizeBytes = row.TotalSizeBytes,
            ExpiredEntries = row.ExpiredEntries,
            InlineEntries = row.InlineEntries,
            FileEntries = row.FileEntries,
            CacheHits = Interlocked.Read(ref _hits),
            CacheMisses = Interlocked.Read(ref _misses),
        };
    }

    /// <inheritdoc/>
    public async Task ClearAsync()
    {
        _ThrowIfNotReady();

        var fileHashes = await _db.GetAllFileHashesAsync(CancellationToken.None).ConfigureAwait(false);
        await using var conn = new SqliteConnection($"Data Source={_options.DatabasePath}");
        await conn.OpenAsync().ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
                          DELETE FROM cache_entries;
                          DELETE FROM instance_cache;
                          DELETE FROM component_cache;
                          VACUUM;
                          """;
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        await _ReleaseFilesAsync(fileHashes).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task CompactAsync()
    {
        _ThrowIfNotReady();
        return _db.CompactAsync(CancellationToken.None);
    }

    #region Event

    public event EventHandler<CacheEntryEvictedEventArgs>? EntryEvicted;

    internal void OnEntryEvicted(CacheEntryEvictedEventArgs args)
        => EntryEvicted?.Invoke(this, args);

    #endregion


    #region Helper

    private void _ThrowIfNotReady()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static DateTime? _ComputeExpiry(CachePolicy policy)
    {
        if (policy.AbsoluteExpiration is null && policy.SlidingExpiration is null)
            return null;  // 永不过期
        if (policy.AbsoluteExpiration is not null)
            return DateTime.UtcNow + policy.AbsoluteExpiration;
        // 仅滑动过期——初始也设一个基于滑动过期的过期点
        return DateTime.UtcNow + policy.SlidingExpiration;
    }

    private static byte[] _Serialize<T>(T value)
    {
        return value switch
        {
            byte[] raw => raw,
            string text => Encoding.UTF8.GetBytes(text),
            _ => JsonSerializer.SerializeToUtf8Bytes(value, _JsonOpts),
        };
    }

    private static T _Deserialize<T>(byte[] data)
    {
        if (typeof(T) == typeof(byte[]))
            return (T)(object)data;
        if (typeof(T) == typeof(string))
            return (T)(object)Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(data, _JsonOpts)!;
    }

    private static string _ComputeSha256(byte[] data)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.TryHashData(data, hash, out _);
        return Convert.ToHexStringLower(hash);
    }

    private async Task _ReleaseFilesAsync(IEnumerable<string> hashes)
    {
        foreach (var hash in hashes)
            await _files.ReleaseAsync(hash).ConfigureAwait(false);
    }

    private sealed class CountingReadStream(Stream inner) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            BytesRead += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            BytesRead += read;
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var read = await inner
                .ReadAsync(buffer, offset, count, cancellationToken)
                .ConfigureAwait(false);
            BytesRead += read;
            return read;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    #endregion

    #region Instance/Component

    public Task UpsertInstanceAsync(InstanceCacheRow row, CancellationToken ct = default)
        => _db.UpsertInstanceAsync(row, ct);

    public Task<InstanceCacheRow?> LookupInstanceAsync(string instancePath, CancellationToken ct = default)
        => _db.LookupInstanceAsync(instancePath, ct);

    public Task DeleteInstanceAsync(string instancePath, CancellationToken ct = default)
        => _db.DeleteInstanceAsync(instancePath, ct);

    public Task UpsertComponentAsync(ComponentCacheRow row, CancellationToken ct = default)
        => _db.UpsertComponentAsync(row, ct);

    public Task<List<ComponentCacheRow>> GetComponentsByInstanceAsync(
        string instancePath, string compType, CancellationToken ct = default)
        => _db.GetComponentsByInstanceAsync(instancePath, compType, ct);

    public Task<ComponentCacheRow?> GetComponentAsync(
        string instancePath, string compType, string fileName, CancellationToken ct = default)
        => _db.GetComponentAsync(instancePath, compType, fileName, ct);

    public Task DeleteComponentsByInstanceAsync(string instancePath, CancellationToken ct = default)
        => _db.DeleteComponentsByInstanceAsync(instancePath, ct);

    public Task<string?> GetComponentScanHashAsync(
        string instancePath, string compType, CancellationToken ct = default)
        => _db.GetComponentScanHashAsync(instancePath, compType, ct);

    #endregion

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _eviction.StopAsync().ConfigureAwait(false);

        try
        {
            await using var conn = new SqliteConnection($"Data Source={_options.DatabasePath}");
            await conn.OpenAsync().ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch { /* ignore */ }

        await CastAndDispose(_db).ConfigureAwait(false);
        await CastAndDispose(_files).ConfigureAwait(false);
        SqliteConnection.ClearAllPools();

        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
            else
                resource.Dispose();
        }
    }
}
