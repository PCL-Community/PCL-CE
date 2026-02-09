using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.App.Cache;

/// <summary>
/// 默认的缓存提供器实现
/// </summary>
/// <typeparam name="TKey">缓存键类型</typeparam>
/// <typeparam name="TEntity">缓存值类型</typeparam>
/// <summary>
/// 默认的缓存提供器实现，基于 <see cref="ConcurrentDictionary{TKey,TValue}"/>。
/// - 支持按时间过期（基于 UTC）
/// - 对过期项采用懒删除（访问或枚举时移除过期项）
/// - 对无过期时间的项使用 <see cref="DateTimeOffset.MaxValue"/> 表示永不过期
/// </summary>
public class DefaultCacheProvider<TKey, TEntity> : ICacheProvider<TKey, TEntity> where TKey : notnull
{
    private record CacheEntity(TEntity Value, DateTimeOffset ExpireTime);

    private readonly ConcurrentDictionary<TKey, CacheEntity> _store = [];

    private bool _disposed;

    /// <inheritdoc/>
    public void AddOrUpdate(TKey key, TEntity value, TimeSpan expriation)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));

        var expiry = DateTimeOffset.UtcNow + expriation;
        var newEntity = new CacheEntity(value, expiry);
        _store.AddOrUpdate(key, newEntity, (_, __) => newEntity);
    }

    /// <inheritdoc/>
    public void AddOrUpdate(TKey key, Func<TKey, TEntity> addFactory, Func<TKey, TEntity, TEntity> updateFactory,
        TimeSpan expiration)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (addFactory is null) throw new ArgumentNullException(nameof(addFactory));
        if (updateFactory is null) throw new ArgumentNullException(nameof(updateFactory));

        var expiry = DateTimeOffset.UtcNow + expiration;
        _store.AddOrUpdate(
            key,
            k => new CacheEntity(addFactory(k), expiry),
            (k, old) => new CacheEntity(updateFactory(k, old.Value), expiry));
    }

    /// <inheritdoc/>
    public bool TryGet(TKey key, [NotNullWhen(true)] out TEntity? value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _EnsureNotDisposed();

        if (_store.TryGetValue(key, out var entity))
        {
            if (DateTimeOffset.UtcNow <= entity.ExpireTime)
            {
                value = entity.Value;
                return true;
            }

            _store.TryRemove(key, out _);
        }

        value = default;
        return false;
    }

    /// <inheritdoc/>
    public IEnumerator<TEntity> GetEnumerator()
    {
        _EnsureNotDisposed();

        foreach (var kv in _store)
        {
            var key = kv.Key;
            var entity = kv.Value;
            if (DateTimeOffset.UtcNow <= entity.ExpireTime)
            {
                yield return entity.Value;
            }
            else
            {
                _store.TryRemove(key, out _);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public bool TryAdd(TKey key, TEntity value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _EnsureNotDisposed();

        var entity = new CacheEntity(value, DateTimeOffset.MaxValue);
        return _store.TryAdd(key, entity);
    }

    /// <inheritdoc/>
    public bool TryAdd(TKey key, TEntity value, TimeSpan expiration)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _EnsureNotDisposed();

        var expiry = DateTimeOffset.UtcNow + expiration;
        var entity = new CacheEntity(value, expiry);
        return _store.TryAdd(key, entity);
    }

    /// <inheritdoc/>
    public void GetOrAdd(TKey key, TEntity value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _EnsureNotDisposed();

        var entity = new CacheEntity(value, DateTimeOffset.MaxValue);
        _store.GetOrAdd(key, entity);
    }

    /// <inheritdoc/>
    public void GetOrAdd(TKey key, Func<TKey, TEntity> valueFactory, TimeSpan expiration)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (valueFactory is null) throw new ArgumentNullException(nameof(valueFactory));
        _EnsureNotDisposed();

        var expiry = DateTimeOffset.UtcNow + expiration;
        _store.GetOrAdd(key, k => new CacheEntity(valueFactory(k), expiry));
    }

    /// <inheritdoc/>
    public void GetOrAdd<TArg>(TKey key, Func<TKey, TArg, TEntity> valueFactory, TimeSpan expiration,
        TArg factoryArg)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (valueFactory is null) throw new ArgumentNullException(nameof(valueFactory));
        _EnsureNotDisposed();

        var expiry = DateTimeOffset.UtcNow + expiration;
        _store.GetOrAdd(key, k => new CacheEntity(valueFactory(k, factoryArg), expiry));
    }

    /// <inheritdoc/>
    public void Remove(TKey key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _EnsureNotDisposed();

        _store.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public bool TryRemove(TKey key)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        _EnsureNotDisposed();

        return _store.TryRemove(key, out _);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _store.Clear();
    }

    private void _EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DefaultCacheProvider<TKey, TEntity>));
    }
}