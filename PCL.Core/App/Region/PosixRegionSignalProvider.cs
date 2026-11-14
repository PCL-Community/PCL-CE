using System;
using System.Globalization;
using System.Linq;
using PCL.Core.Platform;

namespace PCL.Core.App.Region;

public sealed class PosixRegionSignalProvider : IRegionSignalProvider
{
    public RegionSignals ReadSignals()
    {
        var geo = _TryReadUnixGeoName(out var geoAvailable);
        var timeZone = _TryReadLocalTimeZone(out var tzAvailable);
        return new RegionSignals(geo, geoAvailable, timeZone, tzAvailable);
    }

    private static string? _TryReadLocalTimeZone(out bool available)
    {
        available = false;
        try
        {
            var timeZone = TimeZoneInfo.Local.Id;
            if (string.IsNullOrWhiteSpace(timeZone)) return null;
            available = true;
            return timeZone;
        }
        catch
        {
            return null;
        }
    }

    private static string? _TryReadUnixLocale(out bool available)
    {
        available = false;
        foreach (var name in new[]
                 {
                     "LC_ALL",
                     "LC_MESSAGES",
                     "LANG"
                 })
        {
            var normalized = _NormalizeUnixLocale(Environment.GetEnvironmentVariable(name));
            if (normalized is null) continue;
            available = true;
            return normalized;
        }

        var cultureName = CultureInfo.CurrentUICulture.Name;
        if (string.IsNullOrWhiteSpace(cultureName)) return null;
        available = true;
        return cultureName;
    }

    private static string? _TryReadUnixGeoName(out bool available)
    {
        available = false;
        var locale = _TryReadUnixLocale(out var localeAvailable);
        if (!localeAvailable || locale is null) return null;
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
        var modifier = value.IndexOf('.');
        if (modifier >= 0) value = value[..modifier];
        var variant = value.IndexOf('@');
        if (variant >= 0) value = value[..variant];
        if (value.Length == 0
            || value.Equals("C", StringComparison.OrdinalIgnoreCase)
            || value.Equals("POSIX", StringComparison.OrdinalIgnoreCase)) return null;
        return value.Replace('_', '-');
    }
}