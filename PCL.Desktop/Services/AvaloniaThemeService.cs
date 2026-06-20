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
            AccentColor.SkyBlue => "#4890F5",
            AccentColor.System => "#1370F3",
            _ => "#0B5BCB"
        };
        string accentHover = CurrentAccent switch
        {
            AccentColor.SkyBlue => "#96C0F9",
            AccentColor.System => "#4890F5",
            _ => "#1370F3"
        };

        SetBrush("ColorBrush1", isDark ? "#E6EDF5" : "#343D4A");
        SetBrush("ColorBrush2", accent);
        SetBrush("ColorBrush3", accentHover);
        SetBrush("ColorBrush4", isDark ? "#AEBCCD" : "#4890F5");
        SetBrush("ColorBrush5", isDark ? "#8494A8" : "#96C0F9");
        SetBrush("ColorBrush6", isDark ? "#3B4B60" : "#D5E6FD");
        SetBrush("ColorBrush7", isDark ? "#23364D" : "#E0EAFD");
        SetBrush(
            "ColorBrushHalfWhite",
            isDark ? "#14FFFFFF" : "#55FFFFFF");
        SetBrush(
            "ColorBrushTransparentBackground",
            isDark ? "#D21B2736" : "#D2FBFBFB");
        SetBrush("ColorBrushPageBackground", isDark ? "#111A26" : "#FBFBFB");
        SetBrush("ColorBrushPanelBackground", isDark ? "#182536" : "#FBFBFB");
        SetBrush("ColorBrushSidebar", isDark ? "#D2182536" : "#D2FBFBFB");
        SetBrush("ColorBrushSidebarHover", isDark ? "#23364D" : "#E0EAFD");
    }

    private void SetBrush(string key, string color) =>
        _application.Resources[key] =
            new SolidColorBrush(Color.Parse(color));
}
