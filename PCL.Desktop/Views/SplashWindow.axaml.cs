// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace PCL.Desktop.Views;

public sealed partial class SplashWindow : Window
{
    private readonly Stopwatch _fadeClock = new();
    private DispatcherTimer? _fadeTimer;

    public SplashWindow()
    {
        AvaloniaXamlLoader.Load(this);
        Opacity = 1d;
    }

    public void CloseWithFade(TimeSpan duration)
    {
        if (_fadeTimer is not null)
            return;

        _fadeClock.Restart();
        _fadeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _fadeTimer.Tick += (_, _) =>
        {
            double progress = Math.Clamp(_fadeClock.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0d, 1d);
            Opacity = 1d - progress;
            if (progress < 1d)
                return;

            _fadeTimer?.Stop();
            _fadeTimer = null;
            Close();
        };
        _fadeTimer.Start();
    }
}
