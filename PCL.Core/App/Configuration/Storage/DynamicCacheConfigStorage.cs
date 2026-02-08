using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.App.Configuration.Storage;

public class DynamicCacheConfigStorage : ConfigStorage
{
    private readonly Dictionary<object, ConfigStorage> _cache = [];
    private ConfigStorage? _nullContextCache;

    /// <summary>
    /// 存取仓库工厂。在没有匹配的上下文实例时将被调用，以创建新的上下文实例。
    /// </summary>
    public required Func<object?, ConfigStorage> StorageFactory { get; init; }

    protected override bool OnAccess<TKey, TValue>(StorageAction action, ref TKey key, [NotNullWhen(true)] ref TValue value, object? context)
    {
        ConfigStorage? storage;
        if (context == null) storage = _nullContextCache;
        else _cache.TryGetValue(context, out storage);
        if (storage == null)
        {
            try
            {
                storage = StorageFactory(context);
                if (context == null) _nullContextCache = storage;
                else _cache[context] = storage;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to invoke storage factory", ex);
            }
        }
        return storage.Access(action, ref key, ref value, context);
    }

    protected override void OnStop()
    {
        foreach (var item in _cache.Values) item.Stop();
        _cache.Clear();
    }

    public bool InvalidateCache(object context)
    {
        var result = _cache.TryGetValue(context, out var center);
        if (result)
        {
            center?.Stop();
            _cache.Remove(context);
        }
        return result;
    }
}
