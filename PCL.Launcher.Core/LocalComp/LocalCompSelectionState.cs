namespace PCL;

/// <summary>本地资源页共用的选择集合。key 由页面根据 RawPath/path 决定。</summary>
public sealed class LocalCompSelectionState(StringComparer? comparer = null)
{
    private readonly HashSet<string> _selected = new(comparer ?? StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Keys => _selected;

    public int Count => _selected.Count;

    public bool Add(string key)
    {
        return _selected.Add(key);
    }

    public bool Remove(string key)
    {
        return _selected.Remove(key);
    }

    public bool Contains(string key)
    {
        return _selected.Contains(key);
    }

    public void Clear()
    {
        _selected.Clear();
    }

    public void RemoveWhere(Func<string, bool> predicate)
    {
        _selected.RemoveWhere(new Predicate<string>(predicate));
    }

    public void SetSelected(IEnumerable<string> keys, bool selected)
    {
        foreach (var key in keys)
            if (selected)
                _selected.Add(key);
            else
                _selected.Remove(key);
    }
}