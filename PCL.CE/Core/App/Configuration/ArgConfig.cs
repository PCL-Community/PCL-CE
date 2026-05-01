using System;
using PCL.CE.Core.Utils;

namespace PCL.CE.Core.App.Configuration;

public class ArgConfig<TValue> : ParameterizedProperty<object, TValue>
{
    public ArgConfig(Func<object?, TValue> getter, Action<object?, TValue> setter)
    {
        GetValue = getter;
        SetValue = setter;
    }
}
