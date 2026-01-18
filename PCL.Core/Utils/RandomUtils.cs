namespace PCL.Core.Utils;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 提供随机数和集合随机操作的实用方法。
/// </summary>
public static class RandomUtils {
    private static readonly Random SharedRandom = Random.Shared;

    /// <summary>
    /// 从集合中随机选择一个元素。
    /// </summary>
    /// <typeparam name="T">集合元素类型。</typeparam>
    /// <param name="collection">要从中选择元素的集合。</param>
    /// <returns>随机选择的元素。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="collection"/> 为 null 时抛出。</exception>
    /// <exception cref="ArgumentException">当 <paramref name="collection"/> 为空时抛出。</exception>
    public static T PickRandom<T>(ICollection<T> collection) {
        if (collection.Count == 0)
            throw new ArgumentException("集合不能为空", nameof(collection));
        var index = SharedRandom.Next(collection.Count);
        if (collection is IList<T> list)
            return list[index];
        return collection.Skip(index).First();
    }

    /// <summary>
    /// 生成指定范围内的随机整数（包含 min 和 max）。
    /// </summary>
    /// <param name="min">范围下限（包含）。</param>
    /// <param name="max">范围上限（包含）。</param>
    /// <returns>随机整数，范围为 [min, max]。</returns>
    /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="min"/> 大于 <paramref name="max"/> 时抛出。</exception>
    public static int NextInt(int min, int max) {
        return min > max ? throw new ArgumentOutOfRangeException(nameof(min), "最小值不能大于最大值") : SharedRandom.Next(min, max + 1);
    }

    /// <summary>
    /// 随机打乱列表的元素，返回新列表。
    /// </summary>
    /// <typeparam name="T">列表元素类型。</typeparam>
    /// <param name="list">要打乱的列表。</param>
    /// <returns>包含随机顺序元素的新列表。</returns>
    /// <exception cref="ArgumentNullException">当 <paramref name="list"/> 为 null 时抛出。</exception>
    public static List<T> Shuffle<T>(IList<T> list) {
        var result = new List<T>(list);
        var n = result.Count;
        for (var i = n - 1; i > 0; i--) {
            var j = SharedRandom.Next(0, i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return result;
    }

    /// <summary>
    /// 原地随机打乱列表的元素。
    /// </summary>
    /// <typeparam name="T">列表元素类型。</typeparam>
    /// <param name="list">要打乱的列表。</param>
    /// <exception cref="ArgumentNullException">当 <paramref name="list"/> 为 null 时抛出。</exception>
    public static void ShuffleInPlace<T>(IList<T> list) {
        var n = list.Count;
        for (var i = n - 1; i > 0; i--) {
            var j = SharedRandom.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
