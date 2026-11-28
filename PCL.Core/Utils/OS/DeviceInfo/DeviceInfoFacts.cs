using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PCL.Core.IO.Net;
using PCL.Core.Platform;

namespace PCL.Core.Utils.OS.DeviceInfo;

public sealed record DeviceInfoFacts(
    DateTimeOffset CapturedAt,
    OperatingSystemInfo OperatingSystem,
    string OsDescription,
    Architecture OsArchitecture,
    Architecture ProcessArchitecture,
    bool Is64BitOperatingSystem,
    string MachineName,
    string? DeviceModel,
    string? BiosVersion,
    string CpuName,
    int? PhysicalCoreCount,
    int LogicalProcessorCount,
    ulong TotalMemoryBytes,
    ulong AvailableMemoryBytes,
    IReadOnlyList<HardwareInfo.GpuInfo> Gpus,
    string TimeZoneId,
    TimeSpan UtcOffset,
    TimeSpan SystemUptime,
    string CultureName,
    string UiCultureName,
    NetworkInterfaceUtils.IPv6Status IPv6Status,
    bool IsAdministrator,
    string FrameworkDescription,
    string ClrVersion,
    string? DataPath,
    DiskSpaceInfo.Volume? DataVolume,
    string? GamePath,
    DiskSpaceInfo.Volume? GameVolume);