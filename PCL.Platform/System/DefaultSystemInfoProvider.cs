// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.InteropServices;
using PCL.Platform.Abstractions.System;

namespace PCL.Platform.System;

public sealed class DefaultSystemInfoProvider : ISystemInfoProvider
{
    public OperatingSystemInfo GetOperatingSystem() => new(
        RuntimeInformation.OSDescription,
        Environment.OSVersion.VersionString,
        RuntimeInformation.OSArchitecture.ToString(),
        Environment.Is64BitOperatingSystem);

    public MemoryInfo GetMemoryInfo()
    {
        GCMemoryInfo gcMemoryInfo = GC.GetGCMemoryInfo();
        long totalBytes = Math.Max(0, gcMemoryInfo.TotalAvailableMemoryBytes);
        return new MemoryInfo(totalBytes, 0);
    }

    public CpuInfo GetCpuInfo() => new(
        RuntimeInformation.ProcessArchitecture.ToString(),
        Environment.ProcessorCount,
        RuntimeInformation.ProcessArchitecture.ToString());
}
