using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PCL.Core.Utils;

/// <summary>
/// 表示一个可能成功或失败的操作结果。
/// 类似于 Rust 的 Result。
/// </summary>
/// <typeparam name="TOk">成功时返回的值类型</typeparam>
/// <typeparam name="TErr">失败时返回的错误类型</typeparam>
[DebuggerDisplay("{" + nameof(_DebuggerDisplay) + "}")]
public sealed class Result<TOk, TErr>
{
    private readonly TOk? _value;
    private readonly TErr? _error;
    private readonly bool _isSuccess;

    private Result(TOk value)
    {
        _value = value;
        _isSuccess = true;
    }

    private Result(TErr error)
    {
        _error = error;
        _isSuccess = false;
    }

    /// <summary>
    /// 创建一个成功的结果。
    /// </summary>
    public static Result<TOk, TErr> Ok(TOk value) => new(value);

    /// <summary>
    /// 创建一个失败的结果。
    /// </summary>
    public static Result<TOk, TErr> Err(TErr error) => new(error);

    /// <summary>
    /// 是否为成功状态。
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// 是否为失败状态。
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// 获取成功值（仅在 IsSuccess 为 true 时有效）。
    /// </summary>
    public TOk Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of a failed Result.");

    /// <summary>
    /// 获取错误值（仅在 IsFailure 为 true 时有效）。
    /// </summary>
    public TErr Error => IsFailure ? _error! : throw new InvalidOperationException("Cannot access Error of a successful Result.");

    /// <summary>
    /// 使用模式匹配处理成功和失败情况。
    /// </summary>
    public TR Match<TR>(Func<TOk, TR> onSuccess, Func<TErr, TR> onError)
        => IsSuccess ? onSuccess(_value!) : onError(_error!);

    /// <summary>
    /// 如果成功，执行一个动作。
    /// </summary>
    public void IfSuccess(Action<TOk> action)
    {
        if (IsSuccess) action(_value!);
    }

    /// <summary>
    /// 如果失败，执行一个动作。
    /// </summary>
    public void IfFailure(Action<TErr> action)
    {
        if (IsFailure) action(_error!);
    }

    /// <summary>
    /// 将 Result&lt;T, E&gt; 转换为 Result&lt;T2, E&gt;，通过映射成功值。
    /// </summary>
    public Result<T2, TErr> Map<T2>(Func<TOk, T2> mapper)
        => IsSuccess ? Result<T2, TErr>.Ok(mapper(_value!)) : Result<T2, TErr>.Err(_error!);

    /// <summary>
    /// 将 Result&lt;T, E&gt; 转换为 Result&lt;T, E2&gt;，通过映射错误值。
    /// </summary>
    public Result<TOk, TErr2> MapError<TErr2>(Func<TErr, TErr2> mapper)
        => IsSuccess ? Result<TOk, TErr2>.Ok(_value!) : Result<TOk, TErr2>.Err(mapper(_error!));

    /// <summary>
    /// 如果成功，使用函数返回另一个 Result，实现链式调用。
    /// 类似于 Rust 的 and_then。
    /// </summary>
    public Result<T2, TErr> Bind<T2>(Func<TOk, Result<T2, TErr>> mapper)
        => IsSuccess ? mapper(_value!) : Result<T2, TErr>.Err(_error!);

    /// <summary>
    /// 如果失败，使用函数返回另一个 Result。
    /// 类似于 Rust 的 or_else。
    /// </summary>
    public Result<TOk, TErr2> BindError<TErr2>(Func<TErr, Result<TOk, TErr2>> mapper)
        => IsFailure ? mapper(_error!) : Result<TOk, TErr2>.Ok(_value!);

    /// <summary>
    /// 如果失败，提供一个默认值。
    /// </summary>
    public TOk OrElse(TOk defaultValue) => IsSuccess ? _value! : defaultValue;

    /// <summary>
    /// 如果失败，提供一个函数生成默认值。
    /// </summary>
    public TOk OrElse(Func<TOk> defaultValueFactory) => IsSuccess ? _value! : defaultValueFactory();

    /// <summary>
    /// 如果失败，抛出异常。
    /// </summary>
    public TOk Expect(string message) => IsSuccess ? _value! : throw new InvalidOperationException(message);

    /// <summary>
    /// 如果失败，抛出指定的异常。
    /// </summary>
    public TOk UnwrapOrThrow<TException>(Func<TErr, TException> exceptionFactory) where TException : Exception
        => IsSuccess ? _value! : throw exceptionFactory(_error!);

    // 重写 ToString 用于调试
    private string _DebuggerDisplay => IsSuccess ? $"Ok({_value})" : $"Err({_error})";

    public override string ToString() => _DebuggerDisplay;

    // 重写 Equals 和 GetHashCode 以支持比较
    public override bool Equals(object? obj)
    {
        if (obj is not Result<TOk, TErr> other) return false;
        if (IsSuccess != other.IsSuccess) return false;
        return IsSuccess
            ? EqualityComparer<TOk>.Default.Equals(_value, other._value)
            : EqualityComparer<TErr>.Default.Equals(_error, other._error);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(IsSuccess, _value, _error);
    }
}