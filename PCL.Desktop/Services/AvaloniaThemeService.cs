// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using Avalonia.Media;
using Avalonia.Styling;
using PCL.UI.Abstractions;

namespace PCL.Desktop.Services;

public sealed class AvaloniaThemeService : IThemeService
{
    private readonly Avalonia.Application _application;

    public AvaloniaThemeService(Avalonia.Application application)
    {
        _application = application;
        _application.ActualThemeVariantChanged +=
            (_, _) => ApplyPalette();
    }

    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;

    public AccentColor CurrentAccent { get; private set; } =
        AccentColor.CatBlue;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public void Apply(ThemeMode mode, AccentColor accent)
    {
        CurrentMode = mode;
        CurrentAccent =
            OperatingSystem.IsWindows() && accent == AccentColor.System
                ? AccentColor.CatBlue
                : accent;
        _application.RequestedThemeVariant = mode switch
        {
            ThemeMode.Light => ThemeVariant.Light,
            ThemeMode.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
        ApplyPalette();
        ThemeChanged?.Invoke(
            this,
            new ThemeChangedEventArgs(CurrentMode, CurrentAccent));
    }

    private void ApplyPalette()
    {
        bool isDark =
            _application.ActualThemeVariant == ThemeVariant.Dark;
        string accent = CurrentAccent switch
        {
            AccentColor.SkyBlue => "#58A9E8",
            AccentColor.System => "#4A9BDA",
            _ => "#287BC1"
        };
        string accentHover = CurrentAccent switch
        {
            AccentColor.SkyBlue => "#73B9EE",
            AccentColor.System => "#66AEE1",
            _ => "#3F93D7"
        };

        SetBrush("ColorBrush1", isDark ? "#E6EDF5" : "#283E57");
        SetBrush("ColorBrush2", accent);
        SetBrush("ColorBrush3", accentHover);
        SetBrush("ColorBrush4", isDark ? "#AEBCCD" : "#65758B");
        SetBrush("ColorBrush5", isDark ? "#8494A8" : "#8A99AA");
        SetBrush("ColorBrush6", isDark ? "#3B4B60" : "#D7E0EA");
        SetBrush("ColorBrush7", isDark ? "#23364D" : "#EEF6FD");
        SetBrush(
            "ColorBrushHalfWhite",
            isDark ? "#141FFFFFFF" : "#B8FFFFFF");
        SetBrush(
            "ColorBrushTransparentBackground",
            isDark ? "#F21B2736" : "#F7FFFFFF");
        SetBrush("ColorBrushPageBackground", isDark ? "#111A26" : "#F4F7FB");
        SetBrush("ColorBrushPanelBackground", isDark ? "#182536" : "#FFFFFF");
        SetBrush("ColorBrushSidebar", isDark ? "#0B1420" : "#14263D");
        SetBrush("ColorBrushSidebarHover", isDark ? "#1A3048" : "#203A5D");
    }

    private void SetBrush(string key, string color) =>
        _application.Resources[key] =
            new SolidColorBrush(Color.Parse(color));
}
