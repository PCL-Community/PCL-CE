using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PCL.Core.Utils.Exts;

public static class ConcurrentDictionaryExtension
{
    extension<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dict) where TKey: notnull
    {
        public bool CompareAndRemove(TKey key,
            TValue comparison) => ((ICollection<KeyValuePair<TKey, TValue>>)dict).Remove(new KeyValuePair<TKey, TValue>(key, comparison));

        public TValue? UpdateAndGetPrevious(TKey key,
            TValue value)
        {
            TValue? prevValue = default;
            dict.AddOrUpdate(key, _ =>
            {
                prevValue = default;
                return value;
            }, (_, existingValue) =>
            {
                prevValue = existingValue;
                return value;
            });
            return prevValue;
        }
    }
}