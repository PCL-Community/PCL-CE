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
    private const int AnimationTimeIn = 100;
    private const int AnimationTimeOut = 200;

    public static readonly StyledProperty<string> SvgIconProperty =
        AvaloniaProperty.Register<MyMenuItem, string>(nameof(SvgIcon), string.Empty);

    public static readonly StyledProperty<string> IconDataProperty =
        AvaloniaProperty.Register<MyMenuItem, string>(nameof(IconData), string.Empty);

    public static readonly RoutedEvent<RoutedEventArgs> CheckedEvent =
        RoutedEvent.Register<MyMenuItem, RoutedEventArgs>(nameof(Checked), RoutingStrategies.Bubble);

    private readonly string _uuid = Guid.NewGuid().ToString("N");
    private string? _visualStateName;
    private bool _isAttached;

    public MyMenuItem()
    {
        AttachedToVisualTree += (_, _) =>
        {
            _isAttached = true;
            RefreshIcon();
            RefreshColor();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _isAttached = false;
            ModAnimation.AniStop($"MyMenuItem Color {_uuid}");
        };
        PointerEntered += (_, _) => RefreshColor();
        PointerExited += (_, _) => RefreshColor();
        this.GetObservable(IsEnabledProperty).Subscribe(_ => RefreshColor());
        this.GetObservable(SvgIconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(IconDataProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(ForegroundProperty).Subscribe(_ => SyncIconBrush());
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
            SyncIconBrush();
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
            SyncIconBrush();
        }
        catch (FormatException)
        {
            base.Icon = null;
        }
    }

    private (string BackName, string ForeName, int Time) GetVisualState()
    {
        if (!IsEnabled)
            return ("ColorBrushTransparent", "ColorBrushGray5", AnimationTimeOut);
        if (IsPointerOver)
            return ("ColorBrush6", "ColorBrush2", AnimationTimeIn);
        return ("ColorBrushTransparent", "ColorBrush1", AnimationTimeOut);
    }

    private void RefreshColor()
    {
        var (backName, foreName, time) = GetVisualState();
        string visualStateName = $"{backName}|{foreName}";
        if (_visualStateName == visualStateName)
            return;

        _visualStateName = visualStateName;
        if (_isAttached && ModAnimation.AniControlEnabled == 0)
        {
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaColor(this, BackgroundProperty, backName, time),
                    ModAnimation.AaColor(this, ForegroundProperty, foreName, time)
                },
                $"MyMenuItem Color {_uuid}");
            return;
        }

        ModAnimation.AniStop($"MyMenuItem Color {_uuid}");
        Background = LegacyResourceResolver.Brush(this, backName, "#00ffffff");
        Foreground = LegacyResourceResolver.Brush(this, foreName, "#343d4a");
    }

    private void SyncIconBrush()
    {
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
