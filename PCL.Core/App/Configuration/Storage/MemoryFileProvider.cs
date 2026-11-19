using System.Collections.Generic;

namespace PCL.Core.App.Configuration.Storage;

public sealed class MemoryFileProvider(string path) : IKeyValueFileProvider
{
    private readonly Dictionary<string, object?> _values = [];

    public string FilePath { get; } = path;

    public T Get<T>(string key)
    {
        return _values.TryGetValue(key, out var value) && value is T typed
            ? typed
            : throw new KeyNotFoundException(key);
    }

    public void Set<T>(string key, T value)
    {
        _values[key] = value;
    }

    public bool Exists(string key)
    {
        return _values.ContainsKey(key);
    }

    public void Remove(string key)
    {
        _values.Remove(key);
    }

    public void Sync()
    {
    }
}