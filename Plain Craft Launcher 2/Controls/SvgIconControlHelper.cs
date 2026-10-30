using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.UI.Controls.SvgIcon;

namespace PCL;

internal static class SvgIconControlHelper
{
    internal static bool HasSvgIcon(string? icon)
    {
        return !string.IsNullOrWhiteSpace(icon);
    }

    internal static void ApplyVisibility(
        Path legacyIcon,
        SvgIcon svgIcon,
        bool useSvgIcon)
    {
        legacyIcon.Visibility = useSvgIcon ? Visibility.Collapsed : Visibility.Visible;
        svgIcon.Visibility = useSvgIcon ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static void ApplyIcon(Path legacyIcon, SvgIcon svgIcon, string? svgIconName)
    {
        var useSvgIcon = HasSvgIcon(svgIconName);
        svgIcon.Icon = svgIconName ?? string.Empty;
        ApplyVisibility(legacyIcon, svgIcon, useSvgIcon);
    }

    internal static void SetIconBrush(Path legacyIcon, SvgIcon svgIcon, bool useSvgIcon, Brush brush)
    {
        if (useSvgIcon)
            svgIcon.IconBrush = brush;
        else
            legacyIcon.Fill = brush;
    }

    internal static void SetIconResource(Path legacyIcon, SvgIcon svgIcon, bool useSvgIcon, string resourceKey)
    {
        if (useSvgIcon)
            svgIcon.SetResourceReference(SvgIcon.IconBrushProperty, resourceKey);
        else
            legacyIcon.SetResourceReference(Shape.FillProperty, resourceKey);
    }
}