using System.Collections.Concurrent;
using System.Reflection;
using PCL.Core.App.Configuration;
using PCL.Core.Utils.Exts;

namespace PCL;

public partial class ModSetup : IConfigScope
{
    #region 基础

    public IEnumerable<string> CheckScope(IReadOnlySet<string> keys)
    {
        var methods = typeof(ModSetup).GetMethods();
        foreach (var method in methods)
            _methodCache.TryAdd(method.Name, method);
        return methods.Where(method => keys.Contains(method.Name)).Select(method => method.Name);
    }

    public bool Reset(object? argument = null)
    {
        throw new NotSupportedException();
    }

    public bool IsDefault(object? argument = null)
    {
        throw new NotSupportedException();
    }

    public ModSetup()
    {
        ConfigService.RegisterObserver(this, new ConfigObserver(ConfigEvent.Changed, OnConfigChanged));
    }

    private readonly ConcurrentDictionary<string, MethodInfo?> _methodCache = new();

    private void InvokeEventMethod(string key, Func<object> valueGetter)
    {
        var method = _methodCache.GetOrAdd(key, typeof(ModSetup).GetMethod);
        if (method == null) return;
        var para = method.GetParameters();
        if (para.Length < 1) return;
        var paraType = para[0].ParameterType;
        var value = valueGetter();
        var valueType = value.GetType();
        if (valueType != paraType)
        {
            if (valueType.IsEnum) value = (int)value;
            else if (value is string s) value = StringConvertExtension.Convert(s, paraType);
            else if (paraType == typeof(string)) value = value.ConvertToString();
            else
                throw new InvalidCastException(
                    $"{key}: {valueType.FullName} cannot be converted to {paraType.FullName}");
        }

        method.Invoke(this, [value]);
    }

    public void OnConfigChanged(ConfigEventArgs e)
    {
        var key = e.Item.Key;
        InvokeEventMethod(key, () => e.Value ?? GetConfigItem(key).DefaultValueNoType);
    }

    private static ConfigItem GetConfigItem(string key)
    {
        var result = ConfigService.TryGetConfigItemNoType(key, out var item);
        return result ? item! : throw new KeyNotFoundException($"配置项 '{key}' 不存在");
    }

    /// <summary>
    ///     改变某个设置项的值。
    /// </summary>
    public void Set(string key, object value, bool forceReload = false, ModMinecraft.McInstance? instance = null)
    {
        GetConfigItem(key).SetValueNoType(value, instance?.PathInstance);
    }

    /// <summary>
    ///     应用某个设置项的值。
    /// </summary>
    public object Load(string key, bool forceReload = false, ModMinecraft.McInstance? instance = null)
    {
        var value = Get(key, instance);
        InvokeEventMethod(key, () => value);
        return value;
    }
    
    /// <summary>
    /// 写入某个未经加密的设置项。
    /// 若该设置项经过了加密，则会抛出异常。
    /// </summary>
    public void SetSafe(string key, object value, bool forceReload = false, ModMinecraft.McInstance instance = null)
    {
        if (!ConfigService.TryGetConfigItemNoType(key, out ConfigItem item)) return;
        if (item.Source == ConfigSource.SharedEncrypt) throw new InvalidOperationException("禁止写入加密设置项：" + key);
        Set(key, value, forceReload, instance);
    }

    /// <summary>
    /// 获取某个未经加密的设置项的值。
    /// 若该设置项经过了加密，则会抛出异常。
    /// </summary>
    public object GetSafe(string key, ModMinecraft.McInstance instance = null)
    {
        if (!ConfigService.TryGetConfigItemNoType(key, out ConfigItem item)) return null;
        if (item.Source == ConfigSource.SharedEncrypt) throw new InvalidOperationException("禁止读取加密设置项：" + key);
        return Get(key, instance);
    }
    
    /// <summary>
    ///     获取某个设置项的值。
    /// </summary>
    public object Get(string key, ModMinecraft.McInstance? instance = null)
    {
        return GetConfigItem(key).GetValueNoType(instance?.PathInstance);
    }

    /// <summary>
    ///     初始化某个设置项的值。
    /// </summary>
    public void Reset(string key, bool forceReload = false, ModMinecraft.McInstance? instance = null)
    {
        GetConfigItem(key).Reset(instance?.PathInstance);
    }

    /// <summary>
    ///     获取某个设置项的默认值。
    /// </summary>
    public object GetDefault(string key)
    {
        return GetConfigItem(key).DefaultValueNoType;
    }

    /// <summary>
    ///     某个设置项是否从未被设置过。
    /// </summary>
    public bool IsUnset(string key, ModMinecraft.McInstance? instance = null)
    {
        return GetConfigItem(key).IsDefault(instance?.PathInstance);
    }

    #endregion
}
