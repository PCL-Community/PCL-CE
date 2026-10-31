using System.Collections;

namespace PCL;

/// <summary>
///     线程安全的 List。通过在 For Each 循环中使用浅表副本规避多线程操作或移除自身导致的异常。
/// </summary>
public class SafeList<T> : IDisposable, ICollection<T>
{
    private readonly List<T> _internalList;
    private readonly ReaderWriterLockSlim _lock = new();

    public SafeList()
    {
        _internalList = [];
    }

    public SafeList(IEnumerable<T> data)
    {
        _internalList = [.. data];
    }

    public T this[int index]
    {
        get => _internalList[index];
        set => _internalList[index] = value;
    }

    public void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _internalList.Add(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Remove(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            return _internalList.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _internalList.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _internalList.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public bool IsReadOnly => ((ICollection<T>)_internalList).IsReadOnly;

    public bool Contains(T item)
    {
        return ((ICollection<T>)_internalList).Contains(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ((ICollection<T>)_internalList).CopyTo(array, arrayIndex);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ToList().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    public List<T> ToList()
    {
        _lock.EnterReadLock();
        try
        {
            return [.. _internalList];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void RemoveAt(int index)
    {
        _lock.EnterWriteLock();
        try
        {
            _internalList.RemoveAt(index);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}