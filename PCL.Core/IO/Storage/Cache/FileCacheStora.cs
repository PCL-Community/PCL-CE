using PCL.Core.Utils.Hash;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PCL.Core.IO.Storage.Cache;

public class FileCacheStorage : IDisposable
{
    private readonly HashStorage _hashStorage;
    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, int> _refCounts = [];

    public FileCacheStorage(string cacheRoot, bool enableCompression = true)
    {
        _basePath = cacheRoot;
        Directory.CreateDirectory(cacheRoot);

        _hashStorage = new HashStorage(
            cacheRoot,
            SHA256Provider.Instance,
            compressObjects: enableCompression,
            correctMisplacedFile: false,
            prefixLength: 2);
    }

    public async Task<string> StoreAsync(Stream source, string? knownHash = null)
    {
        var hash = await _hashStorage.PutAsync(source, knownHash).ConfigureAwait(false);
        if (hash is not null)
        {
            _refCounts.AddOrUpdate(hash, 1, (_, count) => count + 1);
        }
        return hash!;
    }

    public Stream? Retrieve(string hash) => _hashStorage.Get(hash);

    public string? GetFilePath(string hash)
    {
        var prefix = hash[..2];
        var paht = Path.Combine(_basePath, prefix, hash);
        return File.Exists(paht) ? paht : null;

    }

    public bool Exists(string hash) => _hashStorage.Exists(hash);

    public async Task<bool> ReleaseAsync(string hash)
    {
        // CAS 重试循环：TryGetValue 与随后的更新/移除之间引用计数可能被其他线程改变，
        // 必须用“仅当值仍为 count 时才成功”的原子操作，否则会丢失递减（文件泄漏）或在仍被引用时误删。
        while (true)
        {
            if (!_refCounts.TryGetValue(hash, out var count))
            {
                return false;
            }

            if (count <= 1)
            {
                // 仅当计数仍为 count 时原子移除；若期间被并发 Store/Release 改变则重试。
                if (!_refCounts.TryRemove(new KeyValuePair<string, int>(hash, count)))
                {
                    continue;
                }

                return await _hashStorage.DeleteAsync(hash).ConfigureAwait(false);
            }

            // 仅当计数仍为 count 时原子递减；失败说明值已被并发修改，重试。
            if (_refCounts.TryUpdate(hash, count - 1, count))
            {
                return true;
            }
        }
    }

    public Task<bool> ForceDeleteAsync(string hash)
    {
        _refCounts.TryRemove(hash, out _);
        return _hashStorage.DeleteAsync(hash);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _refCounts.Clear();
    }
}