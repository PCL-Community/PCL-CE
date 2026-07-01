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
        // CAS 重试循环：TryGetValue 与随后的更新/移除之间引用计数可能被其他线程改变，
        // 必须用“仅当值仍为 count 时才成功”的原子操作，否则会丢失递减（文件泄漏）或在仍被引用时误删。
        // 每次 CAS 失败用 SpinWait 退避（渐进自旋→让出/微睡），避免高竞争下的 CPU 热自旋。
        var spin = new SpinWait();
        while (true)
        {
            if (!_refCounts.TryGetValue(hash, out var count))
            {
                return false;
            }

            if (count <= 1)
            {
                // 最后一个引用：仅当计数仍为 count 时原子移除，成功后才删除文件。
                if (TryRemoveRef(hash, count))
                {
                    return await _hashStorage.DeleteAsync(hash).ConfigureAwait(false);
                }
            }
            else if (TryDecrementRef(hash, count))
            {
                return true;
            }

            // CAS 失败：计数已被并发的 Store/Release 修改，退避后重读重试。
            spin.SpinOnce();
        }
    }

    /// <summary>仅当引用计数仍等于 <paramref name="expected"/> 时原子递减，返回是否成功。</summary>
    private bool TryDecrementRef(string hash, int expected) =>
        _refCounts.TryUpdate(hash, expected - 1, expected);

    /// <summary>仅当引用计数仍等于 <paramref name="expected"/> 时原子移除该条目，返回是否成功。</summary>
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