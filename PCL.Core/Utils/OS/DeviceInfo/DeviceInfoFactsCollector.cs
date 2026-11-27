using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.Utils.OS.DeviceInfo;

public static class DeviceInfoFactsCollector
{
    public static DeviceInfoFacts Collect(
        string? dataPath = null,
        string? gamePath = null,
        bool includeGpu = true)
    {
        HardwareInfo.GetCpuInfo();

        var capturedAt = DateTimeOffset.Now;
        var memory = _GetMemoryInfo();
        var totalMemoryBytes = memory.TotalBytes > 0
            ? memory.TotalBytes
            : _ToBytes(HardwareInfo.SystemMemorySize);

        return new DeviceInfoFacts(
            capturedAt,
            _GetOperatingSystemInfo(),
            _GetOsDescription(),
            RuntimeInformation.OSArchitecture,
            RuntimeInformation.ProcessArchitecture,
            Environment.Is64BitOperatingSystem,
            _GetMachineName(),
            _GetDeviceModel(),
            _GetBiosVersion(),
            _GetCpuName(),
            HardwareInfo.PhysicalCoreCount,
            Environment.ProcessorCount,
            totalMemoryBytes,
            memory.AvailableBytes,
            includeGpu ? _GetGpus() : [],
            _GetTimeZoneId(),
            _GetUtcOffset(capturedAt),
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            CultureInfo.CurrentCulture.Name,
            CultureInfo.CurrentUICulture.Name,
            _GetIpv6Status(),
            _GetIsAdministrator(),
            RuntimeInformation.FrameworkDescription,
            Environment.Version.ToString(),
            dataPath,
            DiskSpaceInfo.Get(dataPath),
            gamePath,
            DiskSpaceInfo.Get(gamePath));
    }

    private static OperatingSystemInfo _GetOperatingSystemInfo()
    {
        try
        {
            return ApplicationRuntime.Current.Platform.SystemFacts.GetOperatingSystemInfo();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取系统信息失败");
            return new OperatingSystemInfo(
                RuntimeInformation.OSDescription,
                null,
                null,
                "Unknown",
                Environment.OSVersion.Version.ToString());
        }
    }

    private static string _GetOsDescription()
    {
        try
        {
            return RuntimeInformation.OSDescription;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取系统描述失败");
            return string.Empty;
        }
    }

    private static string _GetMachineName()
    {
        try
        {
            return Environment.MachineName;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取设备名称失败");
            return string.Empty;
        }
    }

    private static string? _GetDeviceModel()
    {
        try
        {
            return ApplicationRuntime.Current.Platform.SystemFacts.GetDeviceModel();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取设备型号失败");
            return null;
        }
    }

    private static string? _GetBiosVersion()
    {
        try
        {
            return ApplicationRuntime.Current.Platform.SystemFacts.GetBiosVersion();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取 BIOS 信息失败");
            return null;
        }
    }

    private static string _GetCpuName()
    {
        var name = HardwareInfo.CpuName;
        return string.IsNullOrWhiteSpace(name)
            || string.Equals(name, "Unknown", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : name.Trim();
    }

    private static SystemMemoryInfo _GetMemoryInfo()
    {
        try
        {
            return ApplicationRuntime.Current.Platform.SystemFacts.GetMemoryInfo();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取内存信息失败");
            return default;
        }
    }

    private static IReadOnlyList<HardwareInfo.GpuInfo> _GetGpus()
    {
        try
        {
            return HardwareInfo.Gpus;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取图形设备信息失败");
            return [];
        }
    }

    private static string _GetTimeZoneId()
    {
        try
        {
            return TimeZoneInfo.Local.Id;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取时区失败");
            return "UTC";
        }
    }

    private static TimeSpan _GetUtcOffset(DateTimeOffset at)
    {
        try
        {
            return TimeZoneInfo.Local.GetUtcOffset(at);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取时区偏移失败");
            return TimeSpan.Zero;
        }
    }

    private static NetworkInterfaceUtils.IPv6Status _GetIpv6Status()
    {
        try
        {
            return NetworkInterfaceUtils.GetIPv6Status();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取 IPv6 状态失败");
            return NetworkInterfaceUtils.IPv6Status.Unknown;
        }
    }

    private static bool _GetIsAdministrator()
    {
        try
        {
            return ApplicationRuntime.Current.Platform.SystemFacts.IsAdministrator;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "DeviceInfo", "读取管理员状态失败");
            return false;
        }
    }

    private static ulong _ToBytes(long mib)
    {
        return mib <= 0L ? 0UL : (ulong)mib * 1024UL * 1024UL;
    }
}