using System;
using System.Collections.Generic;

namespace PCL.Core.Utils.Exts;

public static class EnumerableExtensions
{
    /// <summary>
    /// 对目标集合进行枚举，并传递该元素在集合中的位置
    /// </summary>
    /// <param name="collection">要枚举的集合</param>
    /// <param name="handle">处理函数</param>
    /// <typeparam name="T">类型</typeparam>
    public static void ForEachIndexed<T>(this IEnumerable<T> collection, Action<T, int> handle)
    {
        var i = 0;
        foreach (var element in collection)
        {
            handle.Invoke(element ,i);
            i++;
        }
    }
}