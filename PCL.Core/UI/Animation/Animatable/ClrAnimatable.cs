using System;

namespace PCL.Core.UI.Animation.Animatable;

public sealed class ClrAnimatable<TOwner, T> : IAnimatable
{
    private readonly TOwner _owner;
    private readonly Func<TOwner, T> _getter;
    private readonly Action<TOwner, T> _setter;

    public ClrAnimatable(
        TOwner owner,
        Func<TOwner, T> getter,
        Action<TOwner, T> setter)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _setter = setter ?? throw new ArgumentNullException(nameof(setter));
    }

    public T GetValue() => _getter(_owner);

    public void SetValue(T value) => _setter(_owner, value);

    object? IAnimatable.GetValue() => GetValue();
    void IAnimatable.SetValue(object? value) => SetValue((T)value!);
}