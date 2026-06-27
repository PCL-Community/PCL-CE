// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PathShape = Avalonia.Controls.Shapes.Path;

namespace PCL.Desktop.Controls.Legacy;

public class MyMenuItem : MenuItem
{
    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyMenuItem, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<MyMenuItem, string>(nameof(IconData), string.Empty);

    public static readonly RoutedEvent<RoutedEventArgs> CheckedEvent =
        RoutedEvent.Register<MyMenuItem, RoutedEventArgs>(nameof(Checked), RoutingStrategies.Bubble);

    public MyMenuItem()
    {
        AttachedToVisualTree += (_, _) =>
        {
            RefreshIcon();
            RefreshColor();
        };
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(IconDataProperty).Subscribe(_ => RefreshIcon());
        SubmenuOpened += (_, _) => RaiseEvent(new RoutedEventArgs(CheckedEvent));
    }

    public event EventHandler<RoutedEventArgs>? Checked
    {
        add => AddHandler(CheckedEvent, value);
        remove => RemoveHandler(CheckedEvent, value);
    }

    public string SvgIcon
    {
        get => GetValue(SvgIconProperty);
        set => SetValue(SvgIconProperty, value);
    }

    public string IconData
    {
        get => GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public new object? Icon
    {
        get => IconData;
        set => IconData = value?.ToString() ?? string.Empty;
    }

    private void RefreshIcon()
    {
        if (!string.IsNullOrWhiteSpace(SvgIcon))
        {
            base.Icon = new SvgIcon
            {
                Icon = SvgIcon,
                Width = 14d,
                Height = 14d,
                Stretch = Stretch.Uniform,
                IconBrush = Foreground
            };
            return;
        }

        string data = NormalizeGeometry(IconData);
        if (string.IsNullOrWhiteSpace(data))
        {
            base.Icon = null;
            return;
        }

        try
        {
            base.Icon = new PathShape
            {
                Data = Geometry.Parse(data),
                Width = 14d,
                Height = 14d,
                Stretch = Stretch.Uniform,
                Fill = Foreground,
                Stroke = Foreground,
                StrokeThickness = 0d
            };
        }
        catch (FormatException)
        {
            base.Icon = null;
        }
    }

    private void RefreshColor()
    {
        Color foreground = !IsEnabled
            ? Color.Parse("#8c8c8c")
            : IsPointerOver
                ? Color.Parse("#0b5bcb")
                : Color.Parse("#343d4a");
        Color background = IsPointerOver && IsEnabled
            ? Color.FromArgb(0x18, 0x13, 0x70, 0xf3)
            : Colors.Transparent;

        Foreground = new SolidColorBrush(foreground);
        Background = new SolidColorBrush(background);

        if (base.Icon is SvgIcon svgIcon)
            svgIcon.IconBrush = Foreground;
        if (base.Icon is PathShape path)
        {
            path.Fill = Foreground;
            path.Stroke = Foreground;
        }
    }

    private static string NormalizeGeometry(string value)
    {
        value = value.Trim();
        if (value.StartsWith("F1 ", StringComparison.OrdinalIgnoreCase))
            return value[3..].TrimStart();
        return value;
    }
}
