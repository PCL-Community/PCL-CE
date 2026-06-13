// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Utils.Hash;

namespace PCL.Core.IO.Storage.Cache;

public sealed class FileCacheStorage : IDisposable
{
    private readonly HashStorage _hashStorage;
    private readonly string _basePath;
    private readonly Dictionary<string, int> _refCounts = new(StringComparer.Ordinal);
    private readonly object _refCountLock = new();

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

    public async Task<string> StoreAsync(
        Stream source,
        string? knownHash = null,
        CancellationToken cancellationToken = default)
    {
        var hash = await _hashStorage
            .PutAsync(source, knownHash, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Hash storage did not return a content hash.");

        lock (_refCountLock)
        {
            _refCounts[hash] = _refCounts.GetValueOrDefault(hash) + 1;
        }

        return hash;
    }

    public Stream? Retrieve(string hash) => _hashStorage.Get(hash);

    public void RestoreReferences(IEnumerable<string> hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);

        lock (_refCountLock)
        {
            _refCounts.Clear();
            foreach (var hash in hashes)
                _refCounts[hash] = _refCounts.GetValueOrDefault(hash) + 1;
        }
    }

    public string? GetFilePath(string hash)
    {
        var prefix = hash[..2];
        var path = Path.Combine(_basePath, prefix, hash);
        return File.Exists(path) ? path : null;
    }

    public bool Exists(string hash) => _hashStorage.Exists(hash);

    public async Task<bool> ReleaseAsync(string hash)
    {
        var deleteFile = false;
        lock (_refCountLock)
        {
            if (!_refCounts.TryGetValue(hash, out var count))
                return false;

            if (count <= 1)
            {
                _refCounts.Remove(hash);
                deleteFile = true;
            }
            else
            {
                _refCounts[hash] = count - 1;
            }
        }

        return !deleteFile || await _hashStorage.DeleteAsync(hash).ConfigureAwait(false);
    }

    public Task<bool> ForceDeleteAsync(string hash)
    {
        lock (_refCountLock)
            _refCounts.Remove(hash);
        return _hashStorage.DeleteAsync(hash);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_refCountLock)
            _refCounts.Clear();
    }
}
