using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.App.Configuration;

public struct ConfigValueCache<TValue>()
{
    private TValue? _cachedValue;
    private bool _hasCachedValue = false;

    private readonly ConcurrentDictionary<object, TValue> _cacheWithContext = [];

    /// <summary>
    /// 检查指定上下文参数的缓存是否存在。
    /// </summary>
    /// <param name="argument">上下文参数</param>
    public bool Exists(object? argument = null)
    {
        return (argument == null) ? _hasCachedValue : _cacheWithContext.ContainsKey(argument);
    }

    /// <summary>
    /// 尝试读取缓存值。
    /// </summary>
    /// <param name="value">若已缓存则为输出值，否则为默认值</param>
    /// <param name="argument">上下文参数</param>
    /// <returns>若已缓存则为 <c>true</c>，否则为 <c>false</c></returns>
    public bool TryRead(
        [NotNullWhen(true)] out TValue? value,
        object? argument = null)
    {
        bool result;
        if (argument != null) result = _cacheWithContext.TryGetValue(argument, out value);
        else
        {
            if (_hasCachedValue)
            {
                value = _cachedValue!;
                result = true;
            }
            else
            {
                value = default;
                result = false;
            }
        }
        return result;
    }

    /// <summary>
    /// 写入缓存值。
    /// </summary>
    /// <param name="value">输入值</param>
    /// <param name="argument">上下文参数</param>
    public void Write(TValue value, object? argument = null)
    {
        if (argument != null)
        {
            _cacheWithContext[argument] = value;
            return;
        }
        _hasCachedValue = true;
        _cachedValue = value;
    }

    /// <summary>
    /// 清除缓存值。<br/>
    /// 若缓存存在则清除并返回 <c>true</c>，否则返回 <c>false</c>。
    /// </summary>
    /// <param name="argument">上下文参数</param>
    public bool Invalidate(object? argument)
    {
        if (argument != null) return _cacheWithContext.TryRemove(argument, out _);
        if (!_hasCachedValue) return false;
        _cachedValue = default;
        _hasCachedValue = false;
        return true;
    }

    public void InvalidateAll()
    {
        _cacheWithContext.Clear();
        _cachedValue = default;
        _hasCachedValue = false;
    }
}
