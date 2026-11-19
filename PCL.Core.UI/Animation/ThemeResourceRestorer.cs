using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using PCL.Core.Logging;

namespace PCL.Core.UI.Animation;

/// <summary>颜色动画结束后恢复动态资源绑定，避免主题切换后停留在一次性颜色值。</summary>
internal static class ThemeResourceRestorer
{
    private const string Module = "Animation";

    private static readonly
        ConditionalWeakTable<AvaloniaObject, Dictionary<AvaloniaProperty, IDisposable>>
        Bindings = new();

    public static void Restore(AvaloniaObject target, AvaloniaProperty property, string resourceKey)
    {
        if (target is StyledElement element
            && element.TryFindResource(resourceKey, element.ActualThemeVariant, out _))
        {
            var map = Bindings.GetOrCreateValue(target);
            if (map.Remove(property, out var previous))
                previous.Dispose();

            try
            {
                map[property] = element.Bind(property, new DynamicResourceExtension(resourceKey));
                return;
            }
            catch (Exception ex)
            {
                LogWrapper.Debug(ex, Module, $"颜色动画结束时绑定主题资源失败，改为固定值：{resourceKey}");
            }
        }

        ApplyByThemeVariant(target, property, resourceKey);
    }

    private static void ApplyByThemeVariant(
        AvaloniaObject target,
        AvaloniaProperty property,
        string resourceKey)
    {
        var variant = target is StyledElement element
            ? element.ActualThemeVariant
            : ThemeVariant.Default;

        object? value = null;
        if (target is StyledElement host)
            host.TryFindResource(resourceKey, variant, out value);

        if (value is null && Application.Current is { } application)
            application.TryFindResource(resourceKey, variant, out value);

        var converted = Convert(value, property);
        if (converted is not null)
            target.SetValue(property, converted);
    }

    private static object? Convert(object? value, AvaloniaProperty property)
    {
        var color = value switch
        {
            Color direct => direct,
            ISolidColorBrush brush => brush.Color,
            NColor nColor => nColor.ToColor(),
            _ => (Color?)null
        };
        if (color is null) return null;

        var propertyType = property.PropertyType;
        if (typeof(IBrush).IsAssignableFrom(propertyType))
            return new SolidColorBrush(color.Value);
        return color.Value;
    }
}