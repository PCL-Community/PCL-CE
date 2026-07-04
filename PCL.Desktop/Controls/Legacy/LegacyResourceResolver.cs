// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

internal static class LegacyResourceResolver
{
    public static IBrush Brush(Control control, string resourceKey, string fallback)
    {
        if (TryResolve(control, resourceKey, out object? resource))
        {
            return resource switch
            {
                IBrush brush => brush,
                Color color => new SolidColorBrush(color),
                _ => new SolidColorBrush(Avalonia.Media.Color.Parse(fallback))
            };
        }

        return new SolidColorBrush(Avalonia.Media.Color.Parse(fallback));
    }

    public static Color Color(Control control, string resourceKey, string fallback)
    {
        if (TryResolve(control, resourceKey, out object? resource))
        {
            return resource switch
            {
                Color color => color,
                ISolidColorBrush brush => brush.Color,
                _ => Avalonia.Media.Color.Parse(fallback)
            };
        }

        return Avalonia.Media.Color.Parse(fallback);
    }

    public static Color Color(Control control, string resourceKey, Color fallback) =>
        TryResolve(control, resourceKey, out object? resource)
            ? resource switch
            {
                Color color => color,
                ISolidColorBrush brush => brush.Color,
                _ => fallback
            }
            : fallback;

    public static bool TryResolve(Control control, string resourceKey, out object? resource)
    {
        if (Avalonia.Application.Current is { } application &&
            application.TryGetResource(resourceKey, null, out resource))
        {
            return true;
        }

        return control.TryGetResource(resourceKey, null, out resource);
    }
}
