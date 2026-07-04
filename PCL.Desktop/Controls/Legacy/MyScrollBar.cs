// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public class MyScrollBar : ScrollBar
{
    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private bool _isLoaded;
    private bool _isCaptured;

    protected override Type StyleKeyOverride => typeof(ScrollBar);

    public MyScrollBar()
    {
        AttachedToVisualTree += (_, _) =>
        {
            _isLoaded = true;
            RefreshColor();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isLoaded = false;
            _isCaptured = false;
            ModAnimation.AniStop($"MyScrollBar Color {_uuid}");
        };
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) =>
        {
            _isCaptured = false;
            RefreshColor();
        };
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(IsVisibleProperty).Subscribe(_ => RefreshColor());
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isCaptured = true;
        RefreshColor();
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isCaptured = false;
        RefreshColor();
    }

    private void RefreshColor()
    {
        double newOpacity;
        string newColor;
        int time;
        if (!IsVisible)
        {
            newOpacity = 0d;
            newColor = "ColorBrush4";
            time = 20;
        }
        else if (_isCaptured)
        {
            newOpacity = 1d;
            newColor = "ColorBrush4";
            time = 50;
        }
        else if (IsPointerOver)
        {
            newOpacity = 0.9d;
            newColor = "ColorBrush3";
            time = 130;
        }
        else
        {
            newOpacity = 0.5d;
            newColor = "ColorBrush4";
            time = 180;
        }

        if (_isLoaded && ModAnimation.AniControlEnabled == 0)
        {
            ModAnimation.AniStart(
                new List<ModAnimation.AniData>
                {
                    ModAnimation.AaColor(this, ForegroundProperty, newColor, time),
                    ModAnimation.AaOpacity(this, newOpacity - Opacity, time)
                },
                $"MyScrollBar Color {_uuid}");
            return;
        }

        ModAnimation.AniStop($"MyScrollBar Color {_uuid}");
        Foreground = FindBrush(newColor, "#4890f5");
        Opacity = newOpacity;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}
