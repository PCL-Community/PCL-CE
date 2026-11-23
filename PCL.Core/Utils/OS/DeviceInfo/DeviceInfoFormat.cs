using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS.DeviceInfo;

public static class DeviceInfoFormat
{
    public static string FormatArchitecture(Architecture architecture)
    {
        return architecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "ARM",
            Architecture.Arm64 => "ARM64",
            Architecture.Wasm => "Wasm",
            _ => architecture.ToString()
        };
    }

    public static string FormatUptime(TimeSpan uptime)
    {
        if (uptime < TimeSpan.Zero)
            uptime = TimeSpan.Zero;

        return uptime.TotalDays >= 1d
            ? uptime.ToString(@"d\.hh\:mm\:ss", CultureInfo.InvariantCulture)
            : uptime.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    public static string FormatTimeZone(string? timeZoneId, TimeSpan utcOffset)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? "UTC" : timeZoneId.Trim();
        return $"{id} ({FormatUtcOffset(utcOffset)})";
    }

    public static string FormatUtcOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var absolute = offset.Duration();
        return $"UTC{sign}{absolute.Hours:00}:{absolute.Minutes:00}";
    }

    public static string FormatDiskSpace(
        DiskSpaceInfo.Volume volume,
        IFormatProvider? provider = null)
    {
        ArgumentNullException.ThrowIfNull(volume);
        provider ??= CultureInfo.CurrentCulture;

        const double giB = 1024d * 1024d * 1024d;
        var free = (volume.FreeBytes / giB).ToString("N1", provider);
        var total = (volume.TotalBytes / giB).ToString("N1", provider);
        return $"{free} / {total} GiB";
    }
}