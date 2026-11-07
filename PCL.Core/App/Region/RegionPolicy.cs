using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace PCL.Core.App.Region;

public enum RegionClassification
{
    Unknown,
    Other,
    Mainland
}

public sealed record RegionSignals(
    string? GeoName,
    bool GeoNameAvailable,
    string? TimeZoneId,
    bool TimeZoneAvailable);

public sealed record RegionAssessment(RegionClassification Classification, string Reason)
{
    public bool IsRestricted => Classification == RegionClassification.Mainland;
}

public static partial class RegionPolicy
{
    private static readonly HashSet<string> ChinaTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        "China Standard Time",
        "Asia/Shanghai",
        "PRC"
    };

    public static RegionAssessment Evaluate(RegionSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        // 仅在“时区为中国大陆”且“设备地区为中国大陆”时启用限制。
        // 设备地区取系统“国家或地区”设置，而不是 OOBE 时写入的设备安装地区。
        // 任一项无法确认或不为中国大陆，均不限制。
        var chinaTimeZone = signals.TimeZoneAvailable &&
                            signals.TimeZoneId is not null &&
                            ChinaTimeZones.Contains(signals.TimeZoneId);
        var chinaRegion = signals.GeoNameAvailable && IsChinaGeoName(signals.GeoName);

        if (chinaTimeZone && chinaRegion)
            return new RegionAssessment(
                RegionClassification.Mainland,
                "china-time-zone-and-region");

        if (!signals.TimeZoneAvailable || !signals.GeoNameAvailable)
            return new RegionAssessment(RegionClassification.Unknown, "signals-unavailable");

        return new RegionAssessment(
            RegionClassification.Other,
            "not-china-time-zone-and-region");
    }

    public static RegionSignals ReadWindowsSignals()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Linux/macOS 没有 Windows 的 GeoID 注册表概念，用 locale 与系统时区提供等价信号。
            var unixGeoName = TryReadUnixGeoName(out var unixGeoAvailable);
            var unixTimeZone = TryReadLocalTimeZone(out var unixTimeZoneAvailable);
            return new RegionSignals(
                unixGeoName,
                unixGeoAvailable,
                unixTimeZone,
                unixTimeZoneAvailable);
        }

        var geoName = TryReadGeoName(out var geoAvailable);
        var timeZone = TryReadLocalTimeZone(out var timeZoneAvailable);
        return new RegionSignals(geoName, geoAvailable, timeZone, timeZoneAvailable);
    }

    private static string? TryReadLocalTimeZone(out bool available)
    {
        available = false;
        try
        {
            var timeZone = TimeZoneInfo.Local.Id;
            if (!string.IsNullOrWhiteSpace(timeZone))
            {
                available = true;
                return timeZone;
            }
        }
        catch
        {
            // 继续尝试 /etc/timezone
        }

        if (OperatingSystem.IsLinux())
            try
            {
                var timeZone = File.ReadAllText("/etc/timezone").Trim();
                if (timeZone.Length > 0 && !timeZone.Contains(' ') && timeZone.Contains('/'))
                {
                    available = true;
                    return timeZone;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 没有 /etc/timezone 时保持时区信号不可用
            }

        return null;
    }

    private static string? TryReadUnixLocale(out bool available)
    {
        available = false;
        string[] variables = ["LC_ALL", "LC_MESSAGES", "LANG"];
        foreach (var name in variables)
        {
            var normalized = NormalizeUnixLocale(Environment.GetEnvironmentVariable(name));
            if (normalized is null) continue;
            available = true;
            return normalized;
        }

        var cultureName = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrWhiteSpace(cultureName)) return null;
        available = true;
        return cultureName;
    }

    private static string? TryReadUnixGeoName(out bool available)
    {
        available = false;
        var locale = TryReadUnixLocale(out var localeAvailable);
        if (!localeAvailable || locale is null) return null;

        // 从 locale 的区域段推断国家/地区代码，例如 zh_CN.UTF-8 -> zh-CN -> CN
        var segments = locale.Split('-');
        for (var index = segments.Length - 1; index > 0; index--)
        {
            var segment = segments[index];
            if (segment.Length != 2 || !segment.All(char.IsAsciiLetter)) continue;
            available = true;
            return segment.ToUpperInvariant();
        }

        return null;
    }

    private static string? NormalizeUnixLocale(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        var modifierIndex = value.IndexOf('.');
        if (modifierIndex >= 0) value = value[..modifierIndex];
        var variantIndex = value.IndexOf('@');
        if (variantIndex >= 0) value = value[..variantIndex];
        if (value.Length == 0
            || value.Equals("C", StringComparison.OrdinalIgnoreCase)
            || value.Equals("POSIX", StringComparison.OrdinalIgnoreCase)) return null;
        return value.Replace('_', '-');
    }

    private static bool IsChinaGeoName(string? name) =>
        string.Equals(name, "CN", StringComparison.OrdinalIgnoreCase) || name == "156";

    private static unsafe string? TryReadGeoName(out bool available)
    {
        available = false;
        try
        {
            var buffer = new char[16];
            int length;
            fixed (char* bufferPointer = buffer)
                length = GetUserDefaultGeoName(bufferPointer, buffer.Length);
            if (length > 1 && length <= buffer.Length)
            {
                available = true;
                return new string(buffer, 0, length - 1);
            }

            return TryReadLegacyGeoName(out available);
        }
        catch
        {
            return TryReadLegacyGeoName(out available);
        }
    }

    private static unsafe string? TryReadLegacyGeoName(out bool available)
    {
        available = false;
        try
        {
            var geoId = GetUserGeoID(16); // GEOCLASS_NATION
            if (geoId <= 0) return null;
            var buffer = new char[16];
            int length;
            fixed (char* bufferPointer = buffer)
                length = GetGeoInfoW(geoId, 4, bufferPointer, buffer.Length, 0); // GEO_ISO2
            if (length <= 1 || length > buffer.Length) return null;
            available = true;
            return new string(buffer, 0, length - 1);
        }
        catch
        {
            return null;
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserDefaultGeoName")]
    private static unsafe partial int GetUserDefaultGeoName(char* geoName, int geoNameCount);

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserGeoID")]
    private static partial int GetUserGeoID(int geoClass);

    [LibraryImport("kernel32.dll", EntryPoint = "GetGeoInfoW")]
    private static unsafe partial int GetGeoInfoW(
        int location,
        int geoType,
        char* geoData,
        int dataSize,
        int flags);
}
