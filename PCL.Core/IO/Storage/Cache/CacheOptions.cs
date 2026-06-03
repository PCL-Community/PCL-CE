using System;

namespace PCL.Core.IO.Storage.Cache;

public record CacheOptions
{
    /// <summary>
    /// SQLite database file path.
    /// </summary>
    public required string DatabasePath { get; init; }
    /// <summary>
    /// File cache root directory. (Used for file-mapped storage mode)
    /// </summary>
    public required string FileCacheRoot { get; init; }
    /// <summary>
    /// Max physical cache size. When the total cache size exceeds this limit, the eviction process will be triggered to free up space.<br/>
    /// <b>(Default: 2 GB)</b>
    /// </summary>
    public long MaxCacheSize { get; init; } = 2L * 1024 * 1024 * 1024; // 2 GB
    /// <summary>
    /// SQLite inline storage size limit. Cache entries smaller than or equal to this size will be stored directly in the SQLite database, while larger entries will be stored as file-mapped.<br/>
    /// </summary>
    public int MaxInlineSize { get; init; } = 256 * 1024; // 256 KB
    /// <summary>
    /// Background eviction interval. The cache will automatically check for expired entries and evict them at this interval.<br/>
    /// <b>(Default: 5 minutes)</b>
    /// </summary>
    public TimeSpan EvictionInterval { get; init; } = TimeSpan.FromMinutes(5);
    /// <summary>
    /// Reserve bytes for the cache. This amount of space will always be reserved for the cache, even when eviction is triggered.<br/>
    /// <b>(Default: 256 MB)</b>
    /// </summary>
    public long ReserveBytes { get; init; } = 256L * 1024 * 102; // 256 MB
    /// <summary>
    /// Whether to enable compression for cache entries. (Enabled by default)<br/>
    /// </summary>
    public bool EnableCompression { get; init; } = true;
}