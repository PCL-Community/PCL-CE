// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public class MyComboBoxItem : ComboBoxItem
{
    public static readonly StyledProperty<object?> ToolTipProperty =
        AvaloniaProperty.Register<MyComboBoxItem, object?>(nameof(ToolTip));

    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 300;

    private string? _backColorName;
    private double _fontOpacity = 1d;

    protected override Type StyleKeyOverride => typeof(ComboBoxItem);

    public MyComboBoxItem()
    {
        Padding = new Thickness(6d, 4d);
        PointerMoved += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        PointerReleased += MyComboBoxItem_PointerReleased;
        this.GetObservable(IsSelectedProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(ToolTipProperty).Subscribe(tip => Avalonia.Controls.ToolTip.SetTip(this, tip));
        RefreshColor();
    }

    public object? ToolTip
    {
        get => GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    public int Uuid { get; } = Random.Shared.Next();

    public override string ToString() => Content?.ToString() ?? string.Empty;

    public static implicit operator string(MyComboBoxItem value) =>
        value.Content?.ToString() ?? string.Empty;

    private void RefreshColor()
    {
        string newBackColorName;
        double newFontOpacity;
        int time;
        if (IsSelected)
        {
            newBackColorName = "ColorBrush6";
            newFontOpacity = 1d;
            time = AnimationTimeIn;
        }
        else if (IsPointerOver)
        {
            newBackColorName = "ColorBrush8";
            newFontOpacity = 1d;
            time = AnimationTimeIn;
        }
        else if (IsEnabled)
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 1d;
            time = AnimationTimeOut;
        }
        else
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 0.4d;
            time = AnimationTimeOut;
        }

        if (_backColorName == newBackColorName && Math.Abs(_fontOpacity - newFontOpacity) < 0.001d)
            return;

        _backColorName = newBackColorName;
        _fontOpacity = newFontOpacity;
        if (ControlVisualHelpers.ShouldAnimate(this))
        {
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, newBackColorName, time),
                    ModAnimation.AaOpacity(this, newFontOpacity - Opacity, time)
                },
                "ComboBoxItem Color " + Uuid);
            return;
        }

        ModAnimation.AniStop("ComboBoxItem Color " + Uuid);
        Background = FindBrush(newBackColorName, "#00ffffff");
        Opacity = newFontOpacity;
    }

    private void MyComboBoxItem_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
            e.Handled = false;
    }

    private IBrush FindBrush(string key, string fallback)
    {
        return LegacyResourceResolver.Brush(this, key, fallback);
    }
}
