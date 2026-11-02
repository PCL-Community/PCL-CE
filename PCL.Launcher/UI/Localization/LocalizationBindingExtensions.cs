using System.Reflection;
using Avalonia;
using Avalonia.Data;
using PCL.Core.UI;

namespace PCL;

/// <summary>
///     把 AvaloniaProperty 单向绑定到本地化文本源的辅助方法。
/// </summary>
public static class LocalizationBindingExtensions
{
    extension(AvaloniaObject target)
    {
        public void SetLocalizedValue(
            AvaloniaProperty property,
            string key)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(property);
            target.Bind(property, CreateLocalizationBinding(key));
        }

        public bool TrySetLocalizedValue(
            string propertyName,
            string key)
        {
            var property = FindAvaloniaProperty(target.GetType(), propertyName);
            if (property is null) return false;
            target.SetLocalizedValue(property, key);
            return true;
        }
    }

    internal static AvaloniaProperty? FindAvaloniaProperty(Type type, string propertyName)
    {
        var fieldName = $"{propertyName}Property";
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field?.GetValue(null) is AvaloniaProperty property)
                return property;
        }

        return null;
    }

    public static Binding CreateLocalizationBinding(string key)
    {
        return new Binding(nameof(LocalizationTextSource.Value))
        {
            Source = LocalizationTextSource.Get(key),
            Mode = BindingMode.OneWay
        };
    }
}