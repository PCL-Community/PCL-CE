using System;

namespace PCL.Core.App.Cache;

/// <summary>
/// 标记被缓存的属性。
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public class CachedPropertyAttribute(Type? providerType = null, Type? keyType = null) : Attribute
{
    /// <summary>
    /// 提供者的类型信息
    /// </summary>
    public Type? ProviderType { get; } = providerType;

    /// <summary>
    /// 缓存键的类型信息
    /// </summary>
    public Type? KeyType { get; } = keyType ?? typeof(string);
}