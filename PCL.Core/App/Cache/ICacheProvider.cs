using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.App.Cache;

/// <summary>
/// 定义缓存提供者的接口，提供基本的缓存操作功能。
/// </summary>
/// <typeparam name="TKey">缓存键的类型。</typeparam>
/// <typeparam name="TEntity">缓存值的类型。</typeparam>
public interface ICacheProvider<TKey, TEntity> : IEnumerable<TEntity>, IDisposable
{
    /// <summary>
    /// 添加或更新指定键的缓存项，不设置过期时间。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="value">缓存值。</param>
    /// <param name="expriation">缓存的过期时间</param>
    public void AddOrUpdate(TKey key, TEntity value, TimeSpan expriation);

    /// <summary>
    /// 添加或更新指定键的缓存项，设置过期时间。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="addFactory">添加缓存项构造工厂</param>
    /// <param name="updateFactory">更新缓存项构造工厂</param>
    /// <param name="expiration">缓存的过期时间。</param>
    public void AddOrUpdate(TKey key, Func<TKey, TEntity> addFactory, Func<TKey, TEntity, TEntity> updateFactory,
        TimeSpan expiration);

    /// <summary>
    /// 尝试获取指定键的缓存值。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="value">输出参数，获取到的缓存值；若未获取到则为 <see langword="null"/>。</param>
    /// <returns>如果成功获取到有效的缓存项则返回 <see langword="true"/>；否则返回 <see langword="false"/>。</returns>
    public bool TryGet(TKey key, [NotNullWhen(true)] out TEntity? value);

    /// <summary>
    /// 尝试添加缓存项，不设置过期时间。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="value">缓存值。</param>
    /// <returns>如果成功添加返回 <see langword="true"/>；若键已存在则返回 <see langword="false"/>。</returns>
    public bool TryAdd(TKey key, TEntity value);

    /// <summary>
    /// 尝试添加缓存项，设置过期时间。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="value">缓存值。</param>
    /// <param name="expiration">缓存的过期时间。</param>
    /// <returns>如果成功添加返回 <see langword="true"/>；若键已存在则返回 <see langword="false"/>。</returns>
    public bool TryAdd(TKey key, TEntity value, TimeSpan expiration);

    /// <summary>
    /// 获取或添加指定键的缓存项，不设置过期时间。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="value">如果键不存在，则添加该缓存值。</param>
    public void GetOrAdd(TKey key, TEntity value);

    /// <summary>
    /// 获取或添加指定键的缓存项，使用值工厂方法生成值，设置过期时间。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <param name="valueFactory">用于生成缓存值的工厂方法。</param>
    /// <param name="exciration">缓存的过期时间。</param>
    public void GetOrAdd(TKey key, Func<TKey, TEntity> valueFactory, TimeSpan exciration);

    /// <summary>
    /// 获取或添加指定键的缓存项，使用带有附加参数的值工厂方法生成值，设置过期时间。
    /// </summary>
    /// <typeparam name="TArg">工厂方法附加参数的类型。</typeparam>
    /// <param name="key">缓存键。</param>
    /// <param name="valueFactory">用于生成缓存值的工厂方法。</param>
    /// <param name="exciration">缓存的过期时间。</param>
    /// <param name="factoryArg">传递给工厂方法的附加参数。</param>
    public void GetOrAdd<TArg>(TKey key, Func<TKey, TArg, TEntity> valueFactory, TimeSpan exciration, TArg factoryArg);

    /// <summary>
    /// 删除指定键的缓存项。
    /// </summary>
    /// <param name="key">缓存键。</param>
    public void Remove(TKey key);

    /// <summary>
    /// 尝试删除指定键的缓存项。
    /// </summary>
    /// <param name="key">缓存键。</param>
    /// <returns>如果成功删除返回 <see langword="true"/>；若键不存在则返回 <see langword="false"/>。</returns>
    public bool TryRemove(TKey key);
}