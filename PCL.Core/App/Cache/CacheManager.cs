using System;
using System.Collections.Concurrent;

namespace PCL.Core.App.Cache;

/// <summary>
/// 缓存管理器
/// </summary>
public static class CacheManager
{
    private static readonly ConcurrentDictionary<Type, object> _Providers = [];

    /// <summary>
    /// 默认的缓存提供器类型
    /// </summary>
    public static Type DefauleProviderType { get; set; } = typeof(DefaultCacheProvider<,>);

    /// <summary>
    /// 获取缓存提供器实例
    /// </summary>
    /// <param name="providerType">目标缓存提供器类型</param>
    /// <typeparam name="TKey">缓存键类型</typeparam>
    /// <typeparam name="TEntity">缓存值类型</typeparam>
    /// <returns>找到的缓存提供器</returns>
    /// <exception cref="InvalidOperationException">在无法找到或无法实例化缓存提供器时抛出</exception>
    public static ICacheProvider<TKey, TEntity> GetProvider<TKey, TEntity>(Type? providerType = null)
    {
        var baseType = providerType ?? DefauleProviderType;

        Type concreteType;
        if (baseType.IsGenericTypeDefinition)
        {
            concreteType = baseType.MakeGenericType(typeof(TKey), typeof(TEntity));
        }
        else
        {
            concreteType = baseType;
            if (!typeof(ICacheProvider<TKey, TEntity>).IsAssignableFrom(concreteType))
            {
                throw new InvalidOperationException(
                    $"Type {concreteType.Name} does not implment ICacheProvider<{typeof(TKey).Name}, {typeof(TEntity).Name}>");
            }
        }

        return (ICacheProvider<TKey, TEntity>)_Providers.GetOrAdd(concreteType, type =>
            Activator.CreateInstance(type) ??
            throw new InvalidOperationException($"Cannot instantiate {type.Name}"));
    }
}