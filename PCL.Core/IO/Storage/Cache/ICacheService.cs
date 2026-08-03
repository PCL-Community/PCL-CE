using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.IO.Storage.Cache;

/// <summary>
/// Provides a contract for a cache service.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Write a value to the cache with the specified key and optional cache policy. If the key already exists, it will be overwritten.<br/>
    /// Small data ( &lt; 256KB) is storaged in SQLite inline, while larger data is storaged as file-mapped.
    /// </summary>
    Task SetAsync<T>(string key, T value, CachePolicy? policy = null, CancellationToken ct = default);
    /// <summary>
    /// Read the cache.
    /// </summary>
    /// <returns>The <see cref="CacheResult{T}"/> the tell 'Miss', 'Hit' or 'Expired'</returns>
    Task<CacheResult<T>> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>
    /// Check if a cache entry exists for the specified key. 
    /// </summary>
    /// <param name="key"></param>
    /// <returns><see langword="true"/> if the entry exists and is not expired, <see langword="false"/> otherwise</returns>
    Task<bool> ExistsAsync(string key);
    /// <summary>
    /// Deletes the cache entry for the specified key.
    /// </summary>
    /// <param name="key"></param>
    /// <returns><see langword="true"/> if the entry was found and deleted, <see langword="false"/> otherwise</returns>
    Task<bool> DeleteAsync(string key);

    /// <summary>
    /// Gets the file path for the cached entry with the specified key.
    /// </summary>
    /// <returns>The file path if the entry exists, otherwise null</returns>
    Task<string?> GetCachedFilePathAsync(string key);
    /// <summary>
    /// Cache a file stream with the specified key and optional cache policy. If the key already exists, it will be overwritten.<br/>
    /// </summary>
    /// <returns>The file path</returns>
    Task<string> CacheFileAsync(string key, Stream source, CachePolicy? policy = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes all cache entries belonging to the specified group.
    /// </summary>
    /// <returns>The number of entries deleted</returns>
    Task<int> DeleteByGroupAsync(string groupName);
    /// <summary>
    /// Deletes all cache entries with the specified tag.
    /// </summary>
    /// <param name="tag"></param>
    /// <returns>The number of entries deleted</returns>
    Task<int> DeleteByTagAsync(string tag);
    /// <summary>
    /// Deletes all expired cache entries.
    /// </summary>
    /// <returns>The number of entries deleted</returns>
    Task<int> DeleteExpiredAsync();

    /// <summary>
    /// Gets the cache statistics.
    /// </summary>
    /// <returns></returns>
    Task<CacheStats> GetStatsAsync();
    /// <summary>
    /// Clear all cache entries. Use with caution as this will remove all cached data regardless of expiration or priority.
    /// </summary>
    Task ClearAsync();
    /// <summary>
    /// Compacts the cache by removing any unused space.
    /// </summary>
    Task CompactAsync();

}