using System;

namespace PCL.Core.Utils;

/// <summary>
/// A value with an unknown type during coding and compiling time.
/// </summary>
/// <param name="value">the origin value</param>
/// <param name="isException">whether the value is an exception as the real value has dropped in trouble</param>
public class AnyType(object value, bool isException = false)
{
    /// <summary>
    /// The origin value.
    /// </summary>
    public object OriginValue => value;
    
    /// <summary>
    /// Get the type of the value.
    /// </summary>
    public Type Type => value.GetType();
    
    /// <summary>
    /// Whether the value dropped in trouble.
    /// </summary>
    public bool HasException => isException;
    
    /// <summary>
    /// Get the last exception if <see cref="HasException"/> is <c>true</c>.
    /// </summary>
    public Exception? LastException => (isException && value is Exception ex) ? ex : null;
    
    public object? Value() => isException ? null : value;
    
    /// <summary>
    /// Get the value with an expected type.
    /// </summary>
    /// <typeparam name="T">the expected type</typeparam>
    /// <returns>the typed value</returns>
    /// <exception cref="InvalidCastException">
    /// type cast failed, or the value has dropped in trouble
    /// (see <see cref="HasException"/> and <see cref="LastException"/>)
    /// </exception>
    public T Value<T>() => (T?)Value() ?? throw new InvalidCastException($"The value has dropped into {Type}");

    /// <summary>
    /// Try getting the value with an expected type.
    /// </summary>
    /// <typeparam name="T">the expected type</typeparam>
    /// <returns>the typed value, or <c>null</c> if the type is incorrect</returns>
    public T? Try<T>() => (T?)((value is T) ? value : null);

    public override string ToString() => value.ToString() ?? string.Empty;
    public override int GetHashCode() => value.GetHashCode();
    public override bool Equals(object? obj) => value.Equals(obj);
    
    /// <summary>
    /// Quickly create the instance from a nullable object.
    /// </summary>
    /// <param name="value">the nullable object</param>
    /// <param name="isException">whether the value is an exception as the real value has dropped in trouble</param>
    /// <returns>an <see cref="AnyType"/> instance, or <c>null</c> if provided object is <c>null</c></returns>
    public static AnyType? FromNullable(object? value, bool isException = false)
        => (value == null) ? null : new AnyType(value, isException);
}
