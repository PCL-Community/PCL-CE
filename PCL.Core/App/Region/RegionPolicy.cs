using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

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
    IReadOnlyList<string> PreferredUiLanguages,
    bool PreferredUiLanguagesAvailable,
    string? UserLocaleName,
    bool UserLocaleAvailable,
    string? TimeZoneId,
    bool TimeZoneAvailable,
    int? DeviceRegionGeoId = null,
    bool DeviceRegionAvailable = false);

public sealed record RegionAssessment(RegionClassification Classification, string Reason)
{
    public bool IsRestricted =>
        Classification is RegionClassification.Mainland or RegionClassification.Unknown;
}

public static partial class RegionPolicy
{
    private const int MainlandChinaGeoId = 45;

    private const string DeviceRegionRegistryPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion";

    private const string DeviceRegionValueName = "DeviceRegion";

    private static readonly HashSet<string> _ChinaTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        "China Standard Time",
        "Asia/Shanghai",
        "PRC"
    };

    public static RegionAssessment Evaluate(RegionSignals signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        if (signals.GeoNameAvailable && _IsChinaGeoName(signals.GeoName))
            return new RegionAssessment(RegionClassification.Mainland, "geo-cn");

        // 设备安装（OOBE）时选择的区域，独立于之后可修改的用户区域设置
        if (signals.DeviceRegionAvailable && _IsChinaDeviceRegion(signals.DeviceRegionGeoId))
            return new RegionAssessment(RegionClassification.Mainland, "device-region-cn");

        var chinaTimeZone = signals is { TimeZoneAvailable: true, TimeZoneId: not null }
            && _ChinaTimeZones.Contains(signals.TimeZoneId);
        var hasChinaLocale = signals.UserLocaleAvailable && _IsChinaLocale(signals.UserLocaleName);
        var hasChinaPreferredLanguage = signals.PreferredUiLanguagesAvailable
            && _IsChinaLocale(signals.PreferredUiLanguages.FirstOrDefault());

        if (chinaTimeZone && (hasChinaLocale || hasChinaPreferredLanguage))
            return new RegionAssessment(
                RegionClassification.Mainland,
                "china-time-zone-and-locale");

        if (signals.GeoNameAvailable
            && _IsValidCountryCode(signals.GeoName)
            && !_IsChinaGeoName(signals.GeoName))
            return new RegionAssessment(RegionClassification.Other, "geo-other");

        var hasExplicitOtherLocale =
            (signals.UserLocaleAvailable && _IsExplicitOtherLocale(signals.UserLocaleName))
            || (signals.PreferredUiLanguagesAvailable
                && _IsExplicitOtherLocale(signals.PreferredUiLanguages.FirstOrDefault()));
        return signals.GeoNameAvailable switch
        {
            false when signals.TimeZoneAvailable
                && !chinaTimeZone
                && hasExplicitOtherLocale =>
                new RegionAssessment(
                    RegionClassification.Other,
                    "other-time-zone-and-locale"),

            false when signals is
                {
                    PreferredUiLanguagesAvailable: false,
                    UserLocaleAvailable: false,
                    TimeZoneAvailable: false
                } =>
                new RegionAssessment(
                    RegionClassification.Unknown,
                    "signals-unavailable"),

            _ =>
                new RegionAssessment(
                    RegionClassification.Unknown,
                    "insufficient-signals")
        };
    }

    public static RegionSignals ReadWindowsSignals()
    {
        if (!OperatingSystem.IsWindows())
        {
            // Linux/macOS 没有 Windows 的 GeoID 与设备区域注册表概念（设备区域不可抗力），
            // 用 LANG/LC_ALL 等环境变量与系统时区提供等价的区域信号。
            var unixLocale = _TryReadUnixLocale(out var unixLocaleAvailable);
            var unixLanguages = _TryReadUnixUiLanguages(out var unixLanguagesAvailable);
            var unixGeoName = _TryReadUnixGeoName(out var unixGeoAvailable);
            var unixTimeZone = _TryReadLocalTimeZone(out var unixTimeZoneAvailable);
            return new RegionSignals(
                unixGeoName,
                unixGeoAvailable,
                unixLanguages,
                unixLanguagesAvailable,
                unixLocale,
                unixLocaleAvailable,
                unixTimeZone,
                unixTimeZoneAvailable);
        }

        var geoName = _TryReadGeoName(out var geoAvailable);
        var languages = _TryReadPreferredUiLanguages(out var languagesAvailable);
        var locale = _TryReadUserLocale(out var localeAvailable);
        var deviceRegion = _TryReadDeviceRegion(out var deviceRegionAvailable);
        var timeZone = _TryReadLocalTimeZone(out var timeZoneAvailable);

        return new RegionSignals(
            geoName,
            geoAvailable,
            languages,
            languagesAvailable,
            locale,
            localeAvailable,
            timeZone,
            timeZoneAvailable,
            deviceRegion,
            deviceRegionAvailable);
    }

    private static string? _TryReadLocalTimeZone(out bool available)
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

    private static string? _TryReadUnixLocale(out bool available)
    {
        available = false;
        string[] variables = ["LC_ALL", "LC_MESSAGES", "LANG"];
        foreach (var name in variables)
        {
            var normalized = _NormalizeUnixLocale(Environment.GetEnvironmentVariable(name));
            if (normalized is null) continue;
            available = true;
            return normalized;
        }

        // 未显式设置 locale 变量时，.NET 会按系统配置初始化 CurrentUICulture
        var cultureName = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrWhiteSpace(cultureName)) return null;
        available = true;
        return cultureName;
    }

    private static IReadOnlyList<string> _TryReadUnixUiLanguages(out bool available)
    {
        available = false;
        var languagesVariable = Environment.GetEnvironmentVariable("LANGUAGE");
        if (!string.IsNullOrWhiteSpace(languagesVariable))
        {
            var languages = languagesVariable
                .Split(':', StringSplitOptions.RemoveEmptyEntries)
                .Select(_NormalizeUnixLocale)
                .Where(language => language is not null)
                .Select(language => language!)
                .ToArray();
            if (languages.Length > 0)
            {
                available = true;
                return languages;
            }
        }

        var locale = _TryReadUnixLocale(out var localeAvailable);
        available = localeAvailable;
        return locale is null ? [] : [locale];
    }

    private static string? _TryReadUnixGeoName(out bool available)
    {
        available = false;
        var locale = _TryReadUnixLocale(out var localeAvailable);
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

    private static string? _NormalizeUnixLocale(string? raw)
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

    private static bool _IsChinaGeoName(string? name)
    {
        return string.Equals(name, "CN", StringComparison.OrdinalIgnoreCase) || name == "156";
    }

    // Windows GeoID 与 ISO 3166 数字码不同，45 才是中国大陆
    private static bool _IsChinaDeviceRegion(int? geoId)
    {
        return geoId == MainlandChinaGeoId;
    }

    private static bool _IsValidCountryCode(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length != 2 || !name.All(char.IsAsciiLetter))
            return false;
        try
        {
            _ = new RegionInfo(name);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool _IsChinaLocale(string? localeName)
    {
        if (string.IsNullOrWhiteSpace(localeName)) return false;
        try
        {
            return string.Equals(
                    CultureInfo.GetCultureInfo(localeName.Replace('_', '-')).Name,
                    "zh-CN",
                    StringComparison.OrdinalIgnoreCase)
                || CultureInfo
                    .GetCultureInfo(localeName.Replace('_', '-'))
                    .Name.EndsWith(
                        "-CN",
                        StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static bool _IsExplicitOtherLocale(string? localeName)
    {
        if (string.IsNullOrWhiteSpace(localeName)) return false;
        try
        {
            var culture = CultureInfo.GetCultureInfo(localeName.Replace('_', '-'));
            return !culture.IsNeutralCulture
                && !string.Equals(
                    new RegionInfo(culture.Name).TwoLetterISORegionName,
                    "CN",
                    StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is CultureNotFoundException or ArgumentException)
        {
            return false;
        }
    }

    private static unsafe string? _TryReadGeoName(out bool available)
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

            return _TryReadLegacyGeoName(out available);
        }
        catch
        {
            return _TryReadLegacyGeoName(out available);
        }
    }

    private static unsafe string? _TryReadLegacyGeoName(out bool available)
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

    private static unsafe IReadOnlyList<string> _TryReadPreferredUiLanguages(out bool available)
    {
        available = false;
        try
        {
            uint count = 0;
            uint chars = 0;
            _ = GetUserPreferredUILanguages(8, &count, null, &chars); // MUI_LANGUAGE_NAME
            if (chars is 0 or > 32768) return [];
            var buffer = Marshal.AllocHGlobal(checked((int)chars * sizeof(char)));
            try
            {
                if (GetUserPreferredUILanguages(8, &count, (char*)buffer, &chars) == 0)
                    return [];
                var values = new string((char*)buffer, 0, checked((int)chars));
                available = true;
                return values.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return [];
        }
    }

    private static unsafe string? _TryReadUserLocale(out bool available)
    {
        available = false;
        try
        {
            var buffer = new char[85];
            int length;
            fixed (char* bufferPointer = buffer)
                length = GetUserDefaultLocaleName(bufferPointer, buffer.Length);
            if (length <= 1 || length > buffer.Length) return null;
            available = true;
            return new string(buffer, 0, length - 1);
        }
        catch
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static int? _TryReadDeviceRegion(out bool available)
    {
        available = false;
        RegistryView[] views = [RegistryView.Registry64, RegistryView.Registry32];
        foreach (var view in views)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(DeviceRegionRegistryPath);
                if (key?.GetValue(DeviceRegionValueName) is not { } rawValue) continue;
                var geoId = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                available = true;
                return geoId;
            }
            catch
            {
                // 注册表视图不可访问或值格式异常时，继续尝试下一个视图
            }
        }

        return null;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserDefaultGeoName")]
    private static unsafe partial int GetUserDefaultGeoName(char* geoName, int geoNameCount);

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserDefaultLocaleName")]
    private static unsafe partial int GetUserDefaultLocaleName(
        char* localeName,
        int localeNameCount);

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserPreferredUILanguages")]
    private static unsafe partial int GetUserPreferredUILanguages(
        uint flags,
        uint* languageCount,
        char* languageBuffer,
        uint* bufferChars);

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