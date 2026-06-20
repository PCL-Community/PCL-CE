// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Application.Minecraft.Java;

public readonly record struct JavaRuntimePlatform(
    JavaRuntimeOperatingSystem OperatingSystem,
    JavaRuntimeArchitecture Architecture)
{
    public string ToMojangKey() =>
        OperatingSystem switch
        {
            JavaRuntimeOperatingSystem.Win32 => Architecture switch
            {
                JavaRuntimeArchitecture.X86 => "windows-x86",
                JavaRuntimeArchitecture.Arm64 => "windows-arm64",
                _ => "windows-x64"
            },
            JavaRuntimeOperatingSystem.Linux => Architecture switch
            {
                JavaRuntimeArchitecture.X86 => "linux-i386",
                _ => "linux"
            },
            JavaRuntimeOperatingSystem.MacOs => Architecture == JavaRuntimeArchitecture.Arm64
                ? "mac-os-arm64"
                : "mac-os",
            _ => throw new ArgumentOutOfRangeException(nameof(OperatingSystem))
        };
}
