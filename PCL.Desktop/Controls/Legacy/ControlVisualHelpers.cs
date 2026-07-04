// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace PCL.Desktop.Controls.Legacy;

internal static class ControlVisualHelpers
{
    internal static bool ShouldAnimate(Control control, object? animationOverride = null) =>
        control.IsAttachedToVisualTree() &&
        control.IsVisible &&
        ModAnimation.AniControlEnabled == 0 &&
        !false.Equals(animationOverride);

    internal static void SetCenterScale(Control control, double scale) =>
        SetCenterScale(control, scale, scale);

    internal static void SetCenterScale(Control control, double scaleX, double scaleY)
    {
        control.RenderTransformOrigin = new RelativePoint(0.5d, 0.5d, RelativeUnit.Relative);
        if (control.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform();
            control.RenderTransform = transform;
        }

        transform.ScaleX = scaleX;
        transform.ScaleY = scaleY;
    }

    internal static void AnimateColorOrSetResource(
        Control target,
        AvaloniaProperty property,
        string resourceKey,
        int duration,
        string animationKey,
        bool shouldAnimate)
    {
        if (shouldAnimate)
        {
            ModAnimation.AniStart(
                ModAnimation.AaColor(target, property, resourceKey, duration),
                animationKey);
            return;
        }

        ModAnimation.AniStop(animationKey);
        SetResourceBrush(target, property, resourceKey);
    }

    private static void SetResourceBrush(Control target, AvaloniaProperty property, string resourceKey)
    {
        IBrush brush = LegacyResourceResolver.Brush(target, resourceKey, "#00ffffff");

        if (property == Border.BackgroundProperty && target is Border backgroundBorder)
            backgroundBorder.Background = brush;
        else if (property == Border.BorderBrushProperty && target is Border borderBrushBorder)
            borderBrushBorder.BorderBrush = brush;
        else if (property == TextBlock.ForegroundProperty && target is TextBlock textBlock)
            textBlock.Foreground = brush;
        else if (property.Name == nameof(TemplatedControl.Foreground) && target is TemplatedControl templated)
            templated.Foreground = brush;
        else if (property == Shape.FillProperty && target is Shape fillShape)
            fillShape.Fill = brush;
        else if (property == Shape.StrokeProperty && target is Shape strokeShape)
            strokeShape.Stroke = brush;
        else if (property == SvgIcon.IconBrushProperty && target is SvgIcon svgIcon)
            svgIcon.IconBrush = brush;
        else if (property == MyDropShadow.ColorProperty && target is MyDropShadow shadow && brush is SolidColorBrush solid)
            shadow.Color = solid.Color;
    }
}
