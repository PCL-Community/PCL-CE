using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections;
using System.Collections.Generic;

namespace PCL.Core.Utils;

public static class ListUtils
{
    /// <summary>
    /// 将元素与 List 的混合体拆分为元素组。
    /// </summary>
    [Obsolete("由于非泛型导致的这个方法的存在，计划在未来的版本中移除")]
    public static List<T> GetFullList<T>(IList data)
    {
        var getFullListRet = new List<T>();
        for (int i = 0, loopTo = data.Count - 1; i <= loopTo; i++)
            if (data[i] is ICollection)
                getFullListRet.AddRange((IEnumerable<T>)data[i]);
            else
                getFullListRet.Add(Conversions.ToGenericParameter<T>(data[i]));

        return getFullListRet;
    }
}