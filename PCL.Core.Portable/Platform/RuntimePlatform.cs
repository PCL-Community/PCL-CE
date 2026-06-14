// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Platform;

/// <summary>
/// Operating-system families used by portable feature policies.
/// </summary>
public enum RuntimePlatform
{
    Windows,
    Linux,
    MacOS,
    Other
}

/// <summary>
/// Provides an AOT-safe snapshot of the current operating-system family.
/// </summary>
public static class RuntimePlatformInfo
{
    public static RuntimePlatform Current { get; } = Detect();

    private static RuntimePlatform Detect()
    {
        if (OperatingSystem.IsWindows())
            return RuntimePlatform.Windows;
        if (OperatingSystem.IsLinux())
            return RuntimePlatform.Linux;
        if (OperatingSystem.IsMacOS())
            return RuntimePlatform.MacOS;
        return RuntimePlatform.Other;
    }
}
