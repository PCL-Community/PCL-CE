using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Controls.Common;
using Path = Avalonia.Controls.Shapes.Path;
using SvgIconControl = PCL.UI.Controls.SvgIcon.SvgIcon;

namespace PCL.UI.Controls;

/// <summary>
///     Path 图标与 SvgIcon 的共用辅助方法：在两者之间切换可见性、填充色与颜色过渡。
/// </summary>
internal static class SvgIconControlHelper
{
    internal static bool HasSvgIcon(string? icon)
    {
        return !string.IsNullOrWhiteSpace(icon);
    }

    internal static void ApplyVisibility(Path legacyIcon, SvgIconControl svgIcon, bool useSvgIcon)
    {
        legacyIcon.IsVisible = !useSvgIcon;
        svgIcon.IsVisible = useSvgIcon;
    }

    internal static void ApplyIcon(Path legacyIcon, SvgIconControl svgIcon, string? svgIconName)
    {
        var useSvgIcon = HasSvgIcon(svgIconName);
        svgIcon.Icon = svgIconName ?? string.Empty;
        ApplyVisibility(legacyIcon, svgIcon, useSvgIcon);
    }

    internal static void SetIconBrush(
        Path legacyIcon,
        SvgIconControl svgIcon,
        bool useSvgIcon,
        IBrush? brush)
    {
        if (useSvgIcon) svgIcon.IconBrush = brush;
        else legacyIcon.Fill = brush;
    }

    internal static void SetIconResource(
        Path legacyIcon,
        SvgIconControl svgIcon,
        bool useSvgIcon,
        string resourceKey)
    {
        var brush = ResolveBrush(resourceKey);
        SetIconBrush(legacyIcon, svgIcon, useSvgIcon, brush);
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIconControl svgIcon,
        string resourceKey,
        int duration,
        string? animationKey = null)
    {
        if (!svgIcon.IsVisible) return;
        if (ResolveBrush(resourceKey) is not ISolidColorBrush { Color: var color }) return;
        svgIcon.AnimateIconBrushTo(
            new MyColor(color),
            TimeSpan.FromMilliseconds(duration),
            animationKey: animationKey);
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIconControl svgIcon,
        MyColor color,
        int duration,
        string? animationKey = null)
    {
        if (svgIcon.IsVisible)
            svgIcon.AnimateIconBrushTo(
                color,
                TimeSpan.FromMilliseconds(duration),
                animationKey: animationKey);
    }

    private static IBrush? ResolveBrush(string resourceKey)
    {
        if (Application.Current is { } app
            && app.TryFindResource(resourceKey, out var value)
            && value is IBrush brush)
            return brush;
        return null;
    }
}