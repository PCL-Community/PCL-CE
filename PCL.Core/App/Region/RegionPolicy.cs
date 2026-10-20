using System;
using System.Collections.Generic;
using System.Globalization;
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
    IReadOnlyList<string> PreferredUiLanguages,
    bool PreferredUiLanguagesAvailable,
    string? UserLocaleName,
    bool UserLocaleAvailable,
    string? TimeZoneId,
    bool TimeZoneAvailable);

public sealed record RegionAssessment(RegionClassification Classification, string Reason)
{
    public bool IsRestricted => Classification is RegionClassification.Mainland or RegionClassification.Unknown;
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

        if (signals.GeoNameAvailable && IsChinaGeoName(signals.GeoName))
            return new(RegionClassification.Mainland, "geo-cn");

        var chinaTimeZone = signals.TimeZoneAvailable &&
                            signals.TimeZoneId is not null &&
                            ChinaTimeZones.Contains(signals.TimeZoneId);
        var hasChinaLocale = signals.UserLocaleAvailable && IsChinaLocale(signals.UserLocaleName);
        var hasChinaPreferredLanguage = signals.PreferredUiLanguagesAvailable &&
                                        IsChinaLocale(signals.PreferredUiLanguages.FirstOrDefault());

        if (chinaTimeZone && (hasChinaLocale || hasChinaPreferredLanguage))
            return new(RegionClassification.Mainland, "china-time-zone-and-locale");

        if (signals.GeoNameAvailable && IsValidCountryCode(signals.GeoName) &&
            !IsChinaGeoName(signals.GeoName))
            return new(RegionClassification.Other, "geo-other");

        var hasExplicitOtherLocale = signals.UserLocaleAvailable && IsExplicitOtherLocale(signals.UserLocaleName) ||
                                     signals.PreferredUiLanguagesAvailable &&
                                     IsExplicitOtherLocale(signals.PreferredUiLanguages.FirstOrDefault());
        if (!signals.GeoNameAvailable && signals.TimeZoneAvailable && !chinaTimeZone && hasExplicitOtherLocale)
            return new(RegionClassification.Other, "other-time-zone-and-locale");

        if (!signals.GeoNameAvailable && !signals.PreferredUiLanguagesAvailable &&
            !signals.UserLocaleAvailable && !signals.TimeZoneAvailable)
            return new(RegionClassification.Unknown, "signals-unavailable");

        return new(RegionClassification.Unknown, "insufficient-signals");
    }

    public static RegionSignals ReadWindowsSignals()
    {
        var geoName = TryReadGeoName(out var geoAvailable);
        var languages = TryReadPreferredUiLanguages(out var languagesAvailable);
        var locale = TryReadUserLocale(out var localeAvailable);
        string? timeZone = null;
        var timeZoneAvailable = false;
        try
        {
            timeZone = TimeZoneInfo.Local.Id;
            timeZoneAvailable = !string.IsNullOrWhiteSpace(timeZone);
        }
        catch
        {
        }

        return new RegionSignals(geoName, geoAvailable, languages, languagesAvailable,
            locale, localeAvailable, timeZone, timeZoneAvailable);
    }

    private static bool IsChinaGeoName(string? name) =>
        string.Equals(name, "CN", StringComparison.OrdinalIgnoreCase) || name == "156";

    private static bool IsValidCountryCode(string? name)
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

    private static bool IsChinaLocale(string? localeName)
    {
        if (string.IsNullOrWhiteSpace(localeName)) return false;
        try
        {
            return string.Equals(CultureInfo.GetCultureInfo(localeName.Replace('_', '-')).Name,
                "zh-CN", StringComparison.OrdinalIgnoreCase) ||
                   CultureInfo.GetCultureInfo(localeName.Replace('_', '-')).Name.EndsWith("-CN",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static bool IsExplicitOtherLocale(string? localeName)
    {
        if (string.IsNullOrWhiteSpace(localeName)) return false;
        try
        {
            var culture = CultureInfo.GetCultureInfo(localeName.Replace('_', '-'));
            return !culture.IsNeutralCulture &&
                   !string.Equals(new RegionInfo(culture.Name).TwoLetterISORegionName, "CN",
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is CultureNotFoundException or ArgumentException)
        {
            return false;
        }
    }

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

    private static unsafe IReadOnlyList<string> TryReadPreferredUiLanguages(out bool available)
    {
        available = false;
        try
        {
            uint count = 0;
            uint chars = 0;
            _ = GetUserPreferredUILanguages(8, &count, null, &chars); // MUI_LANGUAGE_NAME
            if (chars == 0 || chars > 32768) return Array.Empty<string>();
            var buffer = Marshal.AllocHGlobal(checked((int)chars * sizeof(char)));
            try
            {
                if (GetUserPreferredUILanguages(8, &count, (char*)buffer, &chars) == 0)
                    return Array.Empty<string>();
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
            return Array.Empty<string>();
        }
    }

    private static unsafe string? TryReadUserLocale(out bool available)
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

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserDefaultGeoName")]
    private static unsafe partial int GetUserDefaultGeoName(char* geoName, int geoNameCount);

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserDefaultLocaleName")]
    private static unsafe partial int GetUserDefaultLocaleName(char* localeName, int localeNameCount);

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserPreferredUILanguages")]
    private static unsafe partial int GetUserPreferredUILanguages(uint flags, uint* languageCount,
        char* languageBuffer, uint* bufferChars);

    [LibraryImport("kernel32.dll", EntryPoint = "GetUserGeoID")]
    private static partial int GetUserGeoID(int geoClass);

    [LibraryImport("kernel32.dll", EntryPoint = "GetGeoInfoW")]
    private static unsafe partial int GetGeoInfoW(int location, int geoType, char* geoData, int dataSize, int flags);
}
