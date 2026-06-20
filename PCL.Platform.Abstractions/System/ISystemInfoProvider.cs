// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Platform.Abstractions.System;

public interface ISystemInfoProvider
{
    OperatingSystemInfo GetOperatingSystem();
    MemoryInfo GetMemoryInfo();
    CpuInfo GetCpuInfo();
}

public sealed record OperatingSystemInfo(
    string Name,
    string Version,
    string Architecture,
    bool Is64Bit);

public sealed record MemoryInfo(
    long TotalBytes,
    long AvailableBytes);

public sealed record CpuInfo(
    string Name,
    int LogicalProcessorCount,
    string Architecture);
