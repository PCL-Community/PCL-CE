using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL;

/// <summary>
///     颜色动画结束后把属性重新绑定到主题资源，使后续主题切换仍能自动更新。
/// </summary>
internal static class ThemeResourceRestorer
{
    private static readonly ConditionalWeakTable<AvaloniaObject, Dictionary<AvaloniaProperty, string>> Targets =
        new();

    public static void Restore(AvaloniaObject target, AvaloniaProperty property, string resourceKey)
    {
        var map = Targets.GetOrCreateValue(target);
        map[property] = resourceKey;
        Apply(target, property, resourceKey);

        if (target is not StyledElement styled || map.Count != 1) return;
        styled.ActualThemeVariantChanged += (_, _) =>
        {
            foreach (var (keyProperty, key) in map)
                Apply(target, keyProperty, key);
        };
    }

    private static void Apply(AvaloniaObject target, AvaloniaProperty property, string resourceKey)
    {
        if (Application.Current is not { } application
            || !application.TryFindResource(resourceKey, out var value))
            return;

        var color = value switch
        {
            Color direct => direct,
            ISolidColorBrush brush => brush.Color,
            MyColor myColor => (Color)myColor,
            _ => (Color?)null
        };
        if (color is null) return;

        var propertyType = property.PropertyType;
        object converted = typeof(IBrush).IsAssignableFrom(propertyType)
            ? new SolidColorBrush(color.Value)
            : propertyType == typeof(MyColor)
                ? new MyColor(color.Value)
                : color.Value;
        target.SetValue(property, converted);
    }
}
