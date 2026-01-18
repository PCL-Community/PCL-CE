using System;

namespace PCL.Core.App.Configuration;

/// <summary>
/// 标记一个 partial 属性，以添加对应配置项并自动生成访问器。
/// </summary>
/// <param name="key">配置键</param>
/// <param name="defaultValue">默认值</param>
/// <param name="source">配置来源</param>
/// <typeparam name="TValue">值类型</typeparam>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ConfigItemAttribute<TValue>(string key, TValue? defaultValue, ConfigSource source = ConfigSource.Shared) : Attribute
{
    public string Key => key;
    public TValue? DefaultValue => defaultValue;
    public ConfigSource Source => source;
}

/// <summary>
/// 标记一个 partial 属性，以添加对应配置项并自动生成访问器。
/// <p> 注意：默认值将通过 <see cref="Activator"/> 调用指定类型的无参构造器来获取，以解决
/// C# attribute 在 2025 年仍然不支持隔壁 JVM 在 2015
/// 年就支持的极其先进的自定义类型参数的问题。因此，值类型必须有公开的无参构造器，否则运行时将会抛出异常。</p>
/// </summary>
/// <param name="key">配置键</param>
/// <param name="source">配置来源</param>
/// <typeparam name="TValue">值类型</typeparam>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AnyConfigItemAttribute<TValue>(string key, ConfigSource source = ConfigSource.Shared) : Attribute
{
    public string Key => key;
    public Type ValueType => typeof(TValue);
    public ConfigSource Source => source;
}

/// <summary>
/// 标记一个 partial 类为配置组，以自动实现 <see cref="IConfigScope"/> 并生成对应的作用域检查方法。
/// </summary>
/// <param name="name">组名，需符合 C# 标识符规范</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConfigGroupAttribute(string name) : Attribute
{
    public string Name => name;
}

/// <summary>
/// 标记一个类型为 <see cref="ConfigEventRegistry"/> 的 public static 属性，以注册配置项事件。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RegisterConfigEventAttribute : Attribute;
