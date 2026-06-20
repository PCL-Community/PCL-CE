// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.App;

public enum ColorMode
{
    Light = 0,
    Dark = 1,
    System = 2
}

public enum ColorTheme
{
    SkyBlue = 0,
    CatBlue = 1,
    DeathBlue = 2,
    HmclBlue = 3,
    SystemAccent = 4
}

public enum UpdateChannel
{
    Release = 0,
    Beta = 1,
    Dev = 2
}

public enum GameWindowSizeMode
{
    Fullscreen = 0,
    Default = 1,
    Launcher = 2,
    Custom = 3,
    Maximized = 4
}

public enum GameProcessPriority
{
    AboveNormal = 0,
    Normal = 1,
    BelowNormal = 2,
    High = 3,
    RealTime = 4
}

public enum LauncherVisibility
{
    ExitImmediately = 0,
    ObsoleteCaseDoNotUse = 1,
    HideAndExit = 2,
    HideAndReopen = 3,
    MinimizeAndReopen = 4,
    DoNothing = 5
}

#pragma warning disable CA1711 // Preserve the existing public API name during assembly migration.
public enum JvmPreferredIpStack
{
    PreferV4 = 0,
    Default = 1,
    PreferV6 = 2
}
#pragma warning restore CA1711

public enum LauncherAutoUpdateBehavior
{
    DownloadAndInstall = 0,
    DownloadAndAnnounce = 1,
    AnnounceOnly = 2,
    Disable = 3
}

public enum LauncherTitleType
{
    None = 0,
    Default = 1,
    Text = 2,
    Image = 3
}
