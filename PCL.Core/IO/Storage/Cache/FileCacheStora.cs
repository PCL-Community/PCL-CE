using PCL.Core.Utils.Hash;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
        var spin = new SpinWait();
        while (true)
        {
            if (!_refCounts.TryGetValue(hash, out var count))
            {
                return false;
            }

            if (count <= 1)
            {
                if (TryRemoveRef(hash, count))
                {
                    return await _hashStorage.DeleteAsync(hash).ConfigureAwait(false);
                }
            }
            else if (TryDecrementRef(hash, count))
            {
                return true;
            }

            spin.SpinOnce();
        }
    }

    private bool TryDecrementRef(string hash, int expected) =>
        _refCounts.TryUpdate(hash, expected - 1, expected);

    private bool TryRemoveRef(string hash, int expected) =>
        _refCounts.TryRemove(new KeyValuePair<string, int>(hash, expected));

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