// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia;
using Avalonia.Controls;
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
