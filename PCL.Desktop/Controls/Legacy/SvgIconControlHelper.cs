// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

internal static class SvgIconControlHelper
{
    internal static bool HasSvgIcon(string? icon) =>
        !string.IsNullOrWhiteSpace(icon);

    internal static void ApplyVisibility(
        PathShape legacyIcon,
        SvgIcon svgIcon,
        bool useSvgIcon)
    {
        legacyIcon.IsVisible = !useSvgIcon;
        svgIcon.IsVisible = useSvgIcon;
    }

    internal static void ApplyIcon(PathShape legacyIcon, SvgIcon svgIcon, string? svgIconName)
    {
        bool useSvgIcon = HasSvgIcon(svgIconName);
        svgIcon.Icon = svgIconName ?? string.Empty;
        ApplyVisibility(legacyIcon, svgIcon, useSvgIcon);
    }

    internal static void SetIconBrush(PathShape legacyIcon, SvgIcon svgIcon, bool useSvgIcon, IBrush brush)
    {
        if (useSvgIcon)
            svgIcon.IconBrush = brush;
        else
            legacyIcon.Fill = brush;
    }

    internal static void SetIconResource(PathShape legacyIcon, SvgIcon svgIcon, bool useSvgIcon, string resourceKey)
    {
        IBrush brush = ResolveBrush(legacyIcon, resourceKey, Brushes.White);
        SetIconBrush(legacyIcon, svgIcon, useSvgIcon, brush);
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIcon svgIcon,
        string resourceKey,
        int duration,
        string? animationKey = null)
    {
        if (!svgIcon.IsVisible)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaColor(svgIcon, SvgIcon.IconBrushProperty, resourceKey, duration),
            animationKey ?? $"SvgIcon Brush {svgIcon.GetHashCode():x}");
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIcon svgIcon,
        Color color,
        int duration,
        string? animationKey = null)
    {
        if (!svgIcon.IsVisible)
            return;

        ModAnimation.AniStart(
            ModAnimation.AaColor(svgIcon, SvgIcon.IconBrushProperty, color, duration),
            animationKey ?? $"SvgIcon Brush {svgIcon.GetHashCode():x}");
    }

    private static IBrush ResolveBrush(PathShape source, string resourceKey, IBrush fallback)
    {
        if (LegacyResourceResolver.TryResolve(source, resourceKey, out object? resource))
        {
            return resource switch
            {
                IBrush brush => brush,
                Color color => new SolidColorBrush(color),
                _ => fallback
            };
        }

        return fallback;
    }
}
