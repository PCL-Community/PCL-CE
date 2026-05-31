using System;

namespace PCL.Core.IO.Storage.Cache;

/// <summary>
/// Represents the caching policy for managing cached items.<br/>
/// <code>CachePolicy.Default = AbsoluteExpiration = TimeSpan.FromHours(1), Normal (Priority), Auto (StorageMode)</code>
/// </summary>
public record CachePolicy
{
    public static readonly CachePolicy Default = new();

    public static readonly CachePolicy NeverExpire = new()
    {
        SlidingExpiration = null,
        AbsoluteExpiration = null,
        Priority = CachePriority.NeverEvict
    };

    /// <summary>
    /// Gets the absolute expiration time for the cached item. (Calculated from the time the item was cached)<br/>
    /// </summary>
    public TimeSpan? AbsoluteExpiration { get; init; } = TimeSpan.FromHours(1);
    /// <summary>
    /// Gets the sliding expiration time for the cached item. (Resets the expiration timer each time the item is accessed)<br/>
    /// </summary>
    public TimeSpan? SlidingExpiration { get; init; }
    /// <summary>
    /// Gets the priority of the cached item. (Determines eviction order when the cache needs to free up space)
    /// </summary>
    public CachePriority Priority { get; init; } = CachePriority.Normal;
    /// <summary>
    /// Gets the storage mode for the cached item.<br/>
    /// Auto &lt;= 256KB: Inline, &gt; 256KB: FileMapped
    /// </summary>
    public CacheStorageMode StorageMode { get; init; } = CacheStorageMode.Auto;
    /// <summary>
    /// Group. Used for grouping related cached items together, allowing for bulk operations like eviction or retrieval based on the group key.
    /// </summary>
    public string? Group { get; init; }
    /// <summary>
    /// Tag. Spilited by <c>','</c>. Used for multi-demension grouping.
    /// </summary>
    public string? Tags { get; init; }
    /// <summary>
    /// Content format version. (Increased when migration is needed.)
    /// </summary>
    public int ContentVersion { get; init; } = 1;
    /// <summary>
    /// The minimum time to live for the cached item. <br/>
    /// Used when the Internet is not stable.<br/>
    /// (Overrides Priority and StorageMode when the net is not stable)
    /// </summary>
    public TimeSpan? MinTimeToLive { get; init; }
}

public enum CachePriority { Low, Normal, High, NeverEvict }
public enum CacheStorageMode { Auto, Inline, FileMapped }