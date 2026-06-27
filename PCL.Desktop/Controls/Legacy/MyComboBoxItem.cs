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
    private string? _backColorName;
    private double _fontOpacity = 1d;

    public MyComboBoxItem()
    {
        Padding = new Thickness(6d, 4d);
        PointerMoved += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        PointerReleased += MyComboBoxItem_PointerReleased;
        this.GetObservable(IsSelectedProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        RefreshColor();
    }

    public override string ToString() => Content?.ToString() ?? string.Empty;

    public static implicit operator string(MyComboBoxItem value) =>
        value.Content?.ToString() ?? string.Empty;

    private void RefreshColor()
    {
        string newBackColorName;
        double newFontOpacity;
        if (IsSelected)
        {
            newBackColorName = "ColorBrush6";
            newFontOpacity = 1d;
        }
        else if (IsPointerOver)
        {
            newBackColorName = "ColorBrush8";
            newFontOpacity = 1d;
        }
        else if (IsEnabled)
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 1d;
        }
        else
        {
            newBackColorName = "ColorBrushTransparent";
            newFontOpacity = 0.4d;
        }

        if (_backColorName == newBackColorName && Math.Abs(_fontOpacity - newFontOpacity) < 0.001d)
            return;

        _backColorName = newBackColorName;
        _fontOpacity = newFontOpacity;
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
        if (this.TryGetResource(key, null, out object? resource) && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallback));
    }
}
