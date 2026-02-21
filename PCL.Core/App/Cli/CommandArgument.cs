using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PCL.Core.App.Cli;

/// <summary>
/// 无泛型的命令行参数模型
/// </summary>
/// <seealso cref="CommandArgument{TValue}"/>
public abstract class CommandArgument
{
    /// <summary>
    /// 参数键
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 参数值文本
    /// </summary>
    public required string ValueText { get; init; }

    /// <summary>
    /// 参数值类型
    /// </summary>
    public abstract ArgumentValueKind ValueKind { get; }

    /// <summary>
    /// 尝试以指定类型获取参数值
    /// </summary>
    /// <param name="value">参数值，若尝试失败则为该类型默认值</param>
    /// <typeparam name="T">参数值的类型</typeparam>
    /// <returns>是否成功，若类型不匹配则失败</returns>
    public abstract bool TryCastValue<T>([NotNullWhen(true)] out T? value);

    public T? CastValue<T>()
    {
        var result = TryCastValue(out T? value);
        return result ? value : throw new InvalidCastException("Value type mismatch or cannot cast");
    }
}

/// <summary>
/// 命令行参数模型
/// </summary>
/// <typeparam name="TValue">参数值的类型</typeparam>
public abstract class CommandArgument<TValue> : CommandArgument
{
    /// <summary>
    /// 从参数值文本中解析参数类型
    /// </summary>
    /// <returns>对应类型的参数值</returns>
    protected abstract TValue ParseValueText();

    private bool _isValueParsed = false;

    /// <summary>
    /// 参数值
    /// </summary>
    public TValue Value
    {
        get
        {
            if (_isValueParsed) return field;
            _isValueParsed = true;
            return field = ParseValueText();
        }
        protected init
        {
            field = value;
            _isValueParsed = true;
        }
    } = default!;

    public override bool TryCastValue<T>([NotNullWhen(true)] out T value)
    {
        if (Value is T v)
        {
            value = v;
            return true;
        }
        value = default!;
        if (typeof(T) == typeof(string))
        {
            Unsafe.As<T, string>(ref value) = ValueText;
#pragma warning disable CS8762 // The analyzer sucks.
            return true;
#pragma warning restore CS8762
        }
        return false;
    }
}
