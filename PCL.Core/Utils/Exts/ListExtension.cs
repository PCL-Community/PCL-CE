using System;
using System.Collections.Generic;

namespace PCL.Core.Utils.Exts;

public static class ListUtils
{
    /// <summary>
    /// 选择最大值对应的对象。
    /// 若没有元素则返回 default(T)。
    /// </summary>
    public static T MaxOf<T, C>(this IEnumerable<T> source, Func<T, C> selector) where C : IComparable<C>
    {
        using (var enumerator = source.GetEnumerator())
        {
            if (!enumerator.MoveNext()) return default(T);
            T maxItem = enumerator.Current;
            C maxValue = selector(maxItem);
            while (enumerator.MoveNext())
            {
                C Value = selector(enumerator.Current);
                if (Value.CompareTo(maxValue) <= 0) continue;
                maxItem = enumerator.Current;
                maxValue = Value;
            }
            return maxItem;
        }
    }

    /// <summary>
    /// 选择最小值对应的对象。
    /// 若没有元素则返回 default(T)。
    /// </summary>
    public static T MinOf<T, C>(this IEnumerable<T> list, Func<T, C> selector) where C : IComparable<C>
    {
        using (var enumerator = list.GetEnumerator())
        {
            if (!enumerator.MoveNext()) return default(T);
            T minItem = enumerator.Current;
            C minValue = selector(minItem);
            while (enumerator.MoveNext())
            {
                C value = selector(enumerator.Current);
                if (value.CompareTo(minValue) >= 0) continue;
                minItem = enumerator.Current;
                minValue = value;
            }
            return minItem;
        }
    }
}

public static class SortUtils {
    /// <summary>
    /// 对列表进行稳定排序，返回新列表。
    /// </summary>
    /// <typeparam name="T">列表元素类型。</typeparam>
    /// <param name="list">要排序的列表。</param>
    /// <param name="comparison">比较器，接收两个对象，若第一个对象应排在前面，则返回 true。</param>
    /// <returns>排序后的新列表。</returns>
    public static List<T> Sort<T>(this IList<T> list, Func<T, T, bool> comparison) {
        // 创建新列表以避免修改原始列表
        var result = new List<T>(list);
        result.Sort(new StableComparer<T>(comparison));
        return result;
    }

    private class StableComparer<T>(Func<T, T, bool> comparison) : IComparer<T> {
        private readonly Func<T, T, bool> _comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));

        public int Compare(T? x, T? y) {
            if (x is null && y is null) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var xComesFirst = _comparison(x, y);
            var yComesFirst = _comparison(y, x);

            if (!xComesFirst && !yComesFirst) return 0; // 相等，保持稳定
            return xComesFirst ? -1 : 1; // x 在前返回 -1，否则返回 1
        }
    }
}
