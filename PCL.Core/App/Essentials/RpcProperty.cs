using System;

namespace PCL.Core.App.Essentials;

public class RpcPropertyOperationFailedException : Exception;

/// <summary>
/// RPC 属性<br/>
/// 大多数时候只需要使用构造方法，其他结构保留供内部使用
/// </summary>
public class RpcProperty
{
    public delegate void GetValueDelegate(out string? outValue);

    public event GetValueDelegate GetValue;

    public delegate void SetValueDelegate(string? value, ref bool success);

    public event SetValueDelegate? SetValue;

    public readonly string Name;
    public readonly bool Settable = true;

    public string? Value
    {
        get
        {
            GetValue.Invoke(out var value);
            return value;
        }
        set
        {
            var success = true;
            SetValue?.Invoke(value, ref success);
            if (!success)
                throw new RpcPropertyOperationFailedException();
        }
    }

    /// <param name="name">属性名称</param>
    /// <param name="onGetValue">默认的 <c>GetValue</c> 回调</param>
    /// <param name="onSetValue">默认的 <c>SetValue</c> 回调</param>
    /// <param name="settable">指定该属性是否可更改，若该值为 <c>false</c> 的同时 <paramref name="onSetValue"/> 为 <c>null</c>，则该属性成为只读属性</param>
    public RpcProperty(string name, Func<string?> onGetValue, Action<string?>? onSetValue = null, bool settable = false)
    {
        Name = name;
        GetValue += (out outValue) => { outValue = onGetValue(); };
        if (onSetValue != null)
        {
            SetValue += (value, ref _) => { onSetValue(value); };
        }
        else if (!settable)
        {
            Settable = false;
            SetValue += (_, ref success) => { success = false; };
        }
    }
}