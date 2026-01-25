using System;

namespace PCL.Core.Utils;

public sealed class AtomicVariable<T>
{
    private T? _value;
    
    public T? Value {
        get => _value;
        set => SetValue(value);
    }

    public bool ReadOnly { get; set; }
    
    public bool Nullable { get; set; }

    public void SetValue(in T? value)
    {
        if (ReadOnly) throw new NotSupportedException("Read-only variable");
        if (!Nullable && value == null) throw new NotSupportedException("Non-null variable");
        _value = value;
    }

    public AtomicVariable(T? value = default, bool readOnly = false, bool? nullable = null)
    {
        Nullable = nullable ?? value == null;
        SetValue(value);
        ReadOnly = readOnly;
    }
}
