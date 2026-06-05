using System;

namespace PCL.Core.Minecraft.Saves.Editing;

/// <summary>
/// 可编辑值的标记联合 —— 表示"不修改"或"修改为某值"。
/// 值类型实现，避免在 <see cref="Editing.SaveChanges"/> 中产生堆分配。
/// </summary>
/// <typeparam name="T">值的类型（必须为值类型）。</typeparam>
public readonly record struct Editable<T> where T : struct
{
    private readonly T _value;
    private readonly bool _hasValue;

    /// <summary>创建一个"修改为指定值"的实例。</summary>
    public Editable(T value)
    {
        _value = value;
        _hasValue = true;
    }

    /// <summary>是否显式设置了新值。</summary>
    public bool HasValue => _hasValue;

    /// <summary>新值。如果 <see cref="HasValue"/> 为 false，则抛出异常。</summary>
    public T Value => _hasValue ? _value : throw new InvalidOperationException("Editable 没有值。");

    /// <summary>有值则返回新值，否则返回 <paramref name="defaultValue"/>。</summary>
    public T GetValueOrDefault(T defaultValue) => _hasValue ? _value : defaultValue;

    /// <summary>尝试获取新值。</summary>
    public bool TryGetValue(out T value)
    {
        value = _value;
        return _hasValue;
    }

    /// <summary>返回值的字符串表示，无值时返回 "&lt;unspecified&gt;"。</summary>
    public override string ToString() => _hasValue ? _value!.ToString() ?? "" : "<unspecified>";
}
