using System;

namespace PCL.Core.App.IoC;

public class PropertyAccessor<TProperty>(Func<TProperty>? getter = null, Action<TProperty>? setter = null)
{
    public TProperty Value
    {
        get => (getter ?? throw new NotSupportedException("Write-only property"))();
        set => (setter ?? throw new NotSupportedException("Read-only property"))(value);
    }

    public bool CanGet => getter != null;
    public bool CanSet => setter != null;
    public bool IsReadOnly => setter == null;
    public bool IsWriteOnly => getter == null;
}
