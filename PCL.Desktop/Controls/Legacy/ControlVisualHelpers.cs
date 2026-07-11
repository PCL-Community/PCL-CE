// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;

namespace PCL.Desktop.Controls.Legacy;

internal static class ControlVisualHelpers
{
    internal static void AnimateListEntrance(Panel panel, string animationKey)
    {
        if (!ShouldAnimate(panel) || panel.Children.Count == 0)
            return;

        Control[] children = panel.Children.Take(30).ToArray();
        foreach (Control child in children)
        {
            child.Opacity = 0d;
            if (child.RenderTransform is not TranslateTransform translate)
            {
                translate = new TranslateTransform();
                child.RenderTransform = translate;
            }
            translate.Y = 8d;
        }

        Dispatcher.UIThread.Post(() =>
        {
            List<ModAnimation.AniData> animations = [];
            int index = 0;
            foreach (Control child in children.Where(panel.Children.Contains))
            {
                int delay = Math.Min(index * 18, 180);
                animations.Add(ModAnimation.AaOpacity(child, 1d, 160, delay));
                animations.Add(ModAnimation.AaTranslateY(
                    child,
                    -8d,
                    220,
                    delay,
                    new ModAnimation.AniEaseOutFluent()));
                index++;
            }

            if (animations.Count > 0)
                ModAnimation.AniStart(animations, animationKey);
        }, DispatcherPriority.Loaded);
    }

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
