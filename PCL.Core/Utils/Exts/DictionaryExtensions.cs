using System.Collections.Generic;

namespace PCL.Core.Utils.Exts;

public static class DictionaryExtensions
{
    public static TValue GetOrDefault<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary,
        TKey key,
        TValue defaultValue = default!)
    {
        return dictionary.GetValueOrDefault(key, defaultValue);
    }

    public static void AddToList<TKey, TValue>(
        this Dictionary<TKey, List<TValue>> dictionary,
        TKey key,
        TValue value)
    {
        if (dictionary.TryGetValue(key, out var list)) list.Add(value);
        else dictionary.Add(key, [value]);
    }
}