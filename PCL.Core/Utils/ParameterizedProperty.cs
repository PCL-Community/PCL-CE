using System;

namespace PCL.Core.Utils;

/// <summary>
/// 用来模拟带一个参数的属性
/// </summary>
/// <typeparam name="TParam">参数值类型</typeparam>
/// <typeparam name="TValue">属性值类型</typeparam>
public class ParameterizedProperty<TParam, TValue>
{
    public Func<TParam, TValue> GetValue { private get; init; } = null!;
    public Action<TParam, TValue> SetValue { private get; init; } = null!;

    public TValue this[TParam param]
    {
        get => GetValue.Invoke(param);
        set => SetValue.Invoke(param, value);
    }
}