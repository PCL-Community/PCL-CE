using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Utils;

/// <summary>
///     集合辅助工具。
/// </summary>
public static class CollectionUtils
{
    public static List<T> FlattenMixedList<T>(IList data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var result = new List<T>();
        foreach (var item in data)
            switch (item)
            {
                case IEnumerable<T> typedCollection:
                    result.AddRange(typedCollection);
                    break;
                case T typedItem:
                    result.Add(typedItem);
                    break;
                default:
                {
                    if (item is not string && item is IEnumerable enumerable)
                        result.AddRange(enumerable.Cast<T>());
                    else
                        result.Add((T)item!);
                    break;
                }
            }

        return result;
    }

    public static List<T> DistinctByComparison<T>(
        ICollection<T> values,
        Func<T, T, bool> isEqual,
        bool keepLast = false)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(isEqual);

        var result = new List<T>();
        var source = values.ToList();
        for (var i = 0; i < source.Count; i++)
        {
            var hasDuplicateLater = keepLast && source.Skip(i + 1).Any(other => isEqual(source[i], other));
            if (hasDuplicateLater) continue;
            if (!keepLast && result.Any(existing => isEqual(existing, source[i]))) continue;
            result.Add(source[i]);
        }

        return result;
    }
}