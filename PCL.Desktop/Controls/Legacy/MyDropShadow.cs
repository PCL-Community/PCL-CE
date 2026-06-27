// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

/// <summary>
/// Avalonia adapter for PCL's WPF MyDropShadow chrome.
/// </summary>
public class MyDropShadow : Border
{
    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<MyDropShadow, Color>(
            nameof(Color),
            Color.FromArgb(0x71, 0x00, 0x00, 0x00));

    public static readonly StyledProperty<double> ShadowRadiusProperty =
        AvaloniaProperty.Register<MyDropShadow, double>(nameof(ShadowRadius), 5d);

    public MyDropShadow()
    {
        Background = Brushes.Transparent;
        this.GetObservable(ColorProperty).Subscribe(_ => RefreshShadow());
        this.GetObservable(ShadowRadiusProperty).Subscribe(_ => RefreshShadow());
        this.GetObservable(CornerRadiusProperty).Subscribe(_ => RefreshShadow());
        RefreshShadow();
    }

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public double ShadowRadius
    {
        get => GetValue(ShadowRadiusProperty);
        set => SetValue(ShadowRadiusProperty, value);
    }

    private void RefreshShadow()
    {
        var shadow = new BoxShadow
        {
            Blur = ShadowRadius,
            Spread = 0d,
            OffsetX = 0d,
            OffsetY = 0d,
            Color = Color
        };
        BoxShadow = new BoxShadows(shadow);
    }
}
