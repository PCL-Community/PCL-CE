using System;

namespace PCL.Core.IO.Storage.Cache.Model;

public record CacheEntry
{
    /// <summary>
    /// SHA-256 (Raw key)
    /// </summary>
    public string CacheKey { get; init; } = string.Empty;
    public EntryType EntryType { get; init; }
    /// <summary>
    /// MIME
    /// </summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>
    /// Cache data (Used in <see cref="EntryType.Inline"/> mode)
    /// </summary>
    public byte[]? Data { get; init; }
    /// <summary>
    /// Original data size
    /// </summary>
    public long DataSize { get; init; }

    /// <summary>
    /// File hash (SHA-256) (Used in <see cref="EntryType.FileRef"/> mode)
    /// </summary>
    public string? FileHash { get; init; }
    /// <summary>
    /// File path (Relative in cache directory) (Used in <see cref="EntryType.FileRef"/> mode)
    /// </summary>
    public string? FilePath { get; init; }

    public string? ContentHash { get; init; }
    /// <summary>
    /// Content format version (Used for migration.)
    /// </summary>
    public int ContentVersion { get; init; }

    public DateTime CachedAt { get; init; }
    public DateTime LastAccessAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public long HitCount { get; init; }

    public string Tags { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    /// <summary>
    /// <c>0 = Low; 1 = Normal; 2 = High; 3 = NeverEvict</c>
    /// </summary>
    public int Priority { get; init; }
}

public enum EntryType
{
    Inline = 0,
    FileRef = 1
}