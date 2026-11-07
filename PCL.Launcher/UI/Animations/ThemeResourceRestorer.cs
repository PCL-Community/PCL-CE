using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using PCL.Core.Logging;
using MyColor = PCL.UI.Controls.Common.MyColor;

namespace PCL;

/// <summary>
///     颜色动画结束后把属性重新绑定到主题资源。
///     优先使用 <see cref="DynamicResourceExtension" /> 绑定：主题切换、资源变化由 Avalonia 资源系统
///     自动重新求值，不再挂 <c>ActualThemeVariantChanged</c> 事件，因此没有订阅泄漏。
///     目标不在资源宿主中或资源缺失时，退回按当前 <see cref="ThemeVariant" /> 解析出的固定值。
///     同一 (元素, 属性) 只保留一个绑定，重新绑定前释放旧句柄。
/// </summary>
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

        _ApplyByThemeVariant(target, property, resourceKey);
    }

    public static void Release(AvaloniaObject target)
    {
        if (!Bindings.TryGetValue(target, out var map)) return;

        foreach (var binding in map.Values)
            binding.Dispose();
        map.Clear();
    }

    private static void _ApplyByThemeVariant(
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

        var converted = _Convert(value, property);
        if (converted is null) return;

        target.SetValue(property, converted);
    }

    private static object? _Convert(object? value, AvaloniaProperty property)
    {
        var color = value switch
        {
            Color direct => direct,
            ISolidColorBrush brush => brush.Color,
            MyColor myColor => (Color)myColor,
            _ => (Color?)null
        };
        if (color is null) return null;

        var propertyType = property.PropertyType;
        if (typeof(IBrush).IsAssignableFrom(propertyType)) return new SolidColorBrush(color.Value);
        if (propertyType == typeof(MyColor)) return new MyColor(color.Value);
        return color.Value;
    }
}