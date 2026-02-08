using System;

namespace PCL.Core.App.IoC;

public class PropertyAccessor<TProperty>(Func<TProperty> getter, Action<TProperty> setter)
{
    public TProperty Value
    {
        get => getter();
        set => setter(value);
    }
}
