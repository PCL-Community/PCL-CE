using System;

namespace PCL.Core.App.Localization;

/// <summary>标记一个 partial 属性，由源生成器生成本地化 getter。</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LocalizedPropertyAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}