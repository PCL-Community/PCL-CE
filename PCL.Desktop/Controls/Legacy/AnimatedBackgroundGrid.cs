// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

/// <summary>
/// Avalonia adapter for PCL's WPF AnimatedBackgroundGrid; animation is layered back in as the port matures.
/// </summary>
public class AnimatedBackgroundGrid : Grid
{
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<AnimatedBackgroundGrid, IBrush?>(nameof(BackgroundBrush));

    public AnimatedBackgroundGrid()
    {
        this.GetObservable(BackgroundBrushProperty).Subscribe(brush =>
        {
            if (brush is not null)
                Background = brush;
        });
    }

    public AnimatedBackgroundGrid(AvaloniaProperty _)
        : this()
    {
    }

    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }
}
