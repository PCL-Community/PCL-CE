using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PCL.Core.Utils;

/// <summary>
///     Thread-safe list wrapper whose enumeration returns a shallow snapshot.
/// </summary>
public sealed class ConcurrentList<T> : ICollection<T>, IReadOnlyList<T>, IDisposable
{
    private readonly List<T> _items;
    private readonly ReaderWriterLockSlim _sync = new();

    public ConcurrentList()
    {
        _items = [];
    }

    public ConcurrentList(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToList();
    }

    public T this[int index]
    {
        get
        {
            _sync.EnterReadLock();
            try
            {
                return _items[index];
            }
            finally
            {
                _sync.ExitReadLock();
            }
        }
        set
        {
            _sync.EnterWriteLock();
            try
            {
                _items[index] = value;
            }
            finally
            {
                _sync.ExitWriteLock();
            }
        }
    }

    public int Count
    {
        get
        {
            _sync.EnterReadLock();
            try
            {
                return _items.Count;
            }
            finally
            {
                _sync.ExitReadLock();
            }
        }
    }

    public bool IsReadOnly => false;

    public void Add(T item)
    {
        _sync.EnterWriteLock();
        try
        {
            _items.Add(item);
        }
        finally
        {
            _sync.ExitWriteLock();
        }
    }

    public bool Remove(T item)
    {
        _sync.EnterWriteLock();
        try
        {
            return _items.Remove(item);
        }
        finally
        {
            _sync.ExitWriteLock();
        }
    }

    public void RemoveAt(int index)
    {
        _sync.EnterWriteLock();
        try
        {
            _items.RemoveAt(index);
        }
        finally
        {
            _sync.ExitWriteLock();
        }
    }

    public void Clear()
    {
        _sync.EnterWriteLock();
        try
        {
            _items.Clear();
        }
        finally
        {
            _sync.ExitWriteLock();
        }
    }

    public bool Contains(T item)
    {
        _sync.EnterReadLock();
        try
        {
            return _items.Contains(item);
        }
        finally
        {
            _sync.ExitReadLock();
        }
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        Snapshot().CopyTo(array, arrayIndex);
    }

    public List<T> Snapshot()
    {
        _sync.EnterReadLock();
        try
        {
            return [.._items];
        }
        finally
        {
            _sync.ExitReadLock();
        }
    }

    public List<T> ToList()
    {
        return Snapshot();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Snapshot().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        _sync.Dispose();
    }
}