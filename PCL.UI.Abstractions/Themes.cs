// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.UI.Abstractions;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public enum AccentColor
{
    CatBlue,
    SkyBlue,
    System
}

public sealed class ThemeChangedEventArgs(
    ThemeMode mode,
    AccentColor accent) : EventArgs
{
    public ThemeMode Mode { get; } = mode;

    public AccentColor Accent { get; } = accent;
}

public interface IThemeService
{
    ThemeMode CurrentMode { get; }

    AccentColor CurrentAccent { get; }

    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    void Apply(ThemeMode mode, AccentColor accent);
}
