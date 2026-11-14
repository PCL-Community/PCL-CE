using System;
using System.Runtime.InteropServices;
using PCL.Core.Platform;

namespace PCL.Core.App.Region;

public sealed partial class WindowsRegionSignalProvider : IRegionSignalProvider
{
    public RegionSignals ReadSignals()
    {
        var geo = _TryReadGeoName(out var geoAvailable);
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

    private static unsafe string? _TryReadGeoName(out bool available)
    {
        available = false;
        try
        {
            var buffer = new char[16];
            int length;
            fixed (char* pointer = buffer)
            {
                length = GetUserDefaultGeoName(pointer, buffer.Length);
            }

            if (length > 1 && length <= buffer.Length)
            {
                available = true;
                return new string(buffer, 0, length - 1);
            }
        }
        catch
        {
            // fall through to legacy API
        }

        return _TryReadLegacyGeoName(out available);
    }

    private static unsafe string? _TryReadLegacyGeoName(out bool available)
    {
        available = false;
        try
        {
            var geoId = GetUserGeoID(16);
            if (geoId <= 0) return null;
            var buffer = new char[16];
            int length;
            fixed (char* pointer = buffer)
            {
                length = GetGeoInfoW(geoId, 4, pointer, buffer.Length, 0);
            }

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