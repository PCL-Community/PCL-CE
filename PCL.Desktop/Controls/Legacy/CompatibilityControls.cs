// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using PathShape = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;

namespace PCL.Desktop.Controls.Legacy;

public class BlurBorder : Border
{
}

public class MediaElement : Control
{
    public static readonly StyledProperty<string?> LoadedBehaviorProperty =
        AvaloniaProperty.Register<MediaElement, string?>(nameof(LoadedBehavior));

    public static readonly StyledProperty<string?> UnloadedBehaviorProperty =
        AvaloniaProperty.Register<MediaElement, string?>(nameof(UnloadedBehavior));

    public static readonly StyledProperty<double> VolumeProperty =
        AvaloniaProperty.Register<MediaElement, double>(nameof(Volume));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<MediaElement, Stretch>(nameof(Stretch), Stretch.Uniform);

    public event EventHandler? MediaEnded;

    public string? LoadedBehavior
    {
        get => GetValue(LoadedBehaviorProperty);
        set => SetValue(LoadedBehaviorProperty, value);
    }

    public string? UnloadedBehavior
    {
        get => GetValue(UnloadedBehaviorProperty);
        set => SetValue(UnloadedBehaviorProperty, value);
    }

    public double Volume
    {
        get => GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    protected void RaiseMediaEnded() => MediaEnded?.Invoke(this, EventArgs.Empty);
}

public class SvgIcon : PathShape
{
    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<SvgIcon, string>(nameof(Icon), string.Empty);

    public static readonly StyledProperty<IBrush?> IconBrushProperty =
        AvaloniaProperty.Register<SvgIcon, IBrush?>(nameof(IconBrush));

    public SvgIcon()
    {
        Stretch = Stretch.Uniform;
        this.GetObservable(IconProperty).Subscribe(_ => RefreshIcon());
        this.GetObservable(IconBrushProperty).Subscribe(brush => Fill = brush);
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public IBrush? IconBrush
    {
        get => GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    private void RefreshIcon()
    {
        Data = Geometry.Parse(IconGeometryMap.Resolve(Icon));
    }
}

internal static class IconGeometryMap
{
    private const string Default = "M4,4 L20,4 L20,20 L4,20 Z";

    public static string Resolve(string? icon)
    {
        var key = (icon ?? string.Empty).Trim().ToLowerInvariant();
        if (key.Length == 0)
            return Default;

        return key switch
        {
            "lucide/x" or "x" => "M6,6 L18,18 M18,6 L6,18",
            "lucide/minus" or "minus" => "M5,12 L19,12",
            "lucide/circle-help" or "circle-help" => "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M9.8,9 A2.2,2.2 0 1 1 12,11.2 L12,13.2 M12,16.8 L12,17",
            "lucide/arrow-left" or "arrow-left" => "M19,12 L5,12 M11,6 L5,12 L11,18",
            "lucide/play" or "play" => "M8,5 L19,12 L8,19 Z",
            "lucide/download" or "download" => "M12,4 L12,15 M7,10 L12,15 L17,10 M5,20 L19,20",
            "lucide/pickaxe" or "pickaxe" => "M14,4 C17,5 19,7 20,10 M13,5 L5,18 M4,20 L8,16",
            "lucide/globe" or "globe" => "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M3,12 L21,12 M12,3 C9,6 8,9 8,12 C8,15 9,18 12,21 M12,3 C15,6 16,9 16,12 C16,15 15,18 12,21",
            "lucide/settings" or "settings" => "M12,8 A4,4 0 1 0 12,16 A4,4 0 1 0 12,8 M12,3 L13.4,5.7 L16.4,6.2 L17,9.2 L19.5,11 L18,13.8 L18.6,16.8 L15.8,18.4 L14.4,21 L11.6,21 L10.2,18.4 L7.4,16.8 L8,13.8 L6.5,11 L9,9.2 L9.6,6.2 L12.6,5.7 Z",
            "lucide/refresh-cw" or "refresh-cw" => "M20,6 L20,12 L14,12 M4,18 L4,12 L10,12 M18,8 A7,7 0 0 0 6,7 M6,16 A7,7 0 0 0 18,17",
            "lucide/arrow-up-to-line" or "arrow-up-to-line" => "M12,19 L12,7 M6,13 L12,7 L18,13 M5,4 L19,4",
            "lucide/flag" or "flag" => "M6,21 L6,4 M6,4 L17,4 L15,9 L20,14 L6,14",
            "lucide/power" or "power" => "M12,3 L12,11 M7,6 A7,7 0 1 0 17,6",
            "lucide/scroll-text" or "scroll-text" => "M6,4 L17,4 A3,3 0 0 1 20,7 L20,20 L8,20 A3,3 0 0 1 5,17 L5,6 A2,2 0 0 1 7,4 M8,8 L16,8 M8,12 L16,12 M8,16 L13,16",
            _ => Default
        };
    }
}
