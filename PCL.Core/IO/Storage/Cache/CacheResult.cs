using System;

namespace PCL.Core.IO.Storage;

/// <summary>
/// The result of a cache lookup, indicating whether the value was found, the value itself if found, and the time it was cached at.
/// </summary>
public readonly record struct CacheResult<T>
{
    public static readonly CacheResult<T> Miss = default;

    public readonly bool Found;
    public readonly T? Value;
    public readonly DateTime CachedAt;

    public static CacheResult<T> Hit(T value, DateTime cachedAt) => new(value, cachedAt);

    private CacheResult(T value, DateTime cachedAt)
    {
        Found = true;
        Value = value;
        CachedAt = cachedAt;
    }
}