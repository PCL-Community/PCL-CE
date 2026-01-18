using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PCL.Core.Utils;

public sealed class ConcurrentSet<T> : IProducerConsumerCollection<T>, ICollection<T> where T: notnull
{
    private readonly ConcurrentDictionary<T, object?> _dictionary = new();

    public bool IsReadOnly => false;
    public int Count => _dictionary.Count;
    object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;
    bool ICollection.IsSynchronized => ((ICollection)_dictionary).IsSynchronized;
    public bool IgnoreDuplicated { get; init; } = false;

    public bool TryAdd(T item)
    {
        if (!_dictionary.TryAdd(item, null) && !IgnoreDuplicated)
            return false;
        return true;
    }

    public bool TryTake([UnscopedRef] out T item)
    {
        while (true)
        {
            var keys = _dictionary.Keys;
            if (keys.Count == 0)
            {
                item = default!;
                return false;
            }
            foreach (var key in keys)
            {
                if (_dictionary.TryRemove(key, out _))
                {
                    item = key;
                    return true;
                }
            }
        }
    }

    public bool Remove(T item) => _dictionary.TryRemove(item, out _);

    public void Add(T item)
    {
        if (!_dictionary.TryAdd(item, null) && !IgnoreDuplicated)
            throw new ArgumentException(nameof(ConcurrentSet<T>) + " 中已存在该元素");
    }

    public void Clear() => _dictionary.Clear();

    public bool Contains(T item) => _dictionary.ContainsKey(item);

    // ReSharper disable once NotDisposedResourceIsReturned
    public IEnumerator<T> GetEnumerator() => _dictionary.Keys.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void CopyTo(T[] array, int index) => _dictionary.Keys.CopyTo(array, index);

    void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

    void ICollection<T>.CopyTo(T[] array, int arrayIndex) => CopyTo(array, arrayIndex);

    public T[] ToArray() => _dictionary.Keys.ToArray();
}