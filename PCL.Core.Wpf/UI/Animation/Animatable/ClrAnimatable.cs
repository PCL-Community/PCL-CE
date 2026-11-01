using System;

namespace PCL.Core.UI.Animation.Animatable;

public sealed class ClrAnimatable<TOwner, T>(
    TOwner owner,
    Func<TOwner, T> getter,
    Action<TOwner, T> setter)
    : IAnimatable
{
    private readonly Func<TOwner, T> _getter =
        getter ?? throw new ArgumentNullException(nameof(getter));

    private readonly TOwner _owner =
        owner ?? throw new ArgumentNullException(nameof(owner));

    private readonly Action<TOwner, T> _setter =
        setter ?? throw new ArgumentNullException(nameof(setter));

    object? IAnimatable.GetValue()
    {
        return GetValue();
    }

    void IAnimatable.SetValue(object? value)
    {
        SetValue((T)value!);
    }

    void IAnimatable.SetValue<TValue>(TValue value)
    {
        SetValue((T)(object)value!);
    }

    public T GetValue()
    {
        return _getter(_owner);
    }

    public void SetValue(T value)
    {
        _setter(_owner, value);
    }
}