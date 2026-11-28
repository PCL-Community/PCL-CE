using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public sealed class WindowsSystemFacts : ISystemFacts
{
    private static readonly Lazy<WindowsDetails> LazyDetails = new(ReadDetails);

    public SystemMemoryInfo GetMemoryInfo()
    {
        var (total, available) = KernelInterop.GetPhysicalMemoryBytes();
        return new SystemMemoryInfo(total, available);
    }

    public bool IsAdministrator => ProcessInterop.IsAdmin();

    public OperatingSystemInfo GetOperatingSystemInfo()
    {
        return LazyDetails.Value.OperatingSystem;
    }

    public string? GetDeviceModel()
    {
        return LazyDetails.Value.DeviceModel;
    }

    public string? GetBiosVersion()
    {
        return LazyDetails.Value.BiosVersion;
    }

    private static WindowsDetails ReadDetails()
    {
        string? productName = null;
        string? displayVersion = null;
        string? build = null;
        string? deviceModel = null;
        string? biosVersion = null;

        try
        {
            using var versionKey = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            productName = ReadString(versionKey, "ProductName");
            displayVersion = ReadString(versionKey, "DisplayVersion")
                ?? ReadString(versionKey, "ReleaseId");
            var buildNumber = ReadString(versionKey, "CurrentBuildNumber")
                ?? ReadString(versionKey, "CurrentBuild");
            var ubr = ReadString(versionKey, "UBR");
            if (!string.IsNullOrWhiteSpace(buildNumber))
                build = int.TryParse(
                    ubr,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var updateBuild)
                    ? $"{buildNumber}.{updateBuild}"
                    : buildNumber;
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            using var biosKey = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\BIOS");
            var manufacturer = ReadString(biosKey, "SystemManufacturer");
            var product = ReadString(biosKey, "SystemProductName");
            var systemVersion = ReadString(biosKey, "SystemVersion");
            deviceModel = CombineParts(manufacturer, product);
            if (string.IsNullOrWhiteSpace(deviceModel))
                deviceModel = CombineParts(manufacturer, systemVersion);

            var bios = ReadString(biosKey, "BIOSVersion");
            var biosDate = ReadString(biosKey, "BIOSReleaseDate");
            biosVersion = string.IsNullOrWhiteSpace(bios)
                ? null
                : string.IsNullOrWhiteSpace(biosDate)
                    ? bios
                    : $"{bios} ({biosDate})";
        }
        catch (Exception)
        {
            // ignored
        }

        var isWindows11 = int.TryParse(
                build?.Split('.')[0],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var majorBuild)
            && majorBuild >= 22000;
        if (isWindows11
            && productName?.Contains("Windows 10", StringComparison.OrdinalIgnoreCase) is true)
            productName = productName.Replace(
                "Windows 10",
                "Windows 11",
                StringComparison.OrdinalIgnoreCase);

        var osInfo = new OperatingSystemInfo(
            string.IsNullOrWhiteSpace(productName)
                ? RuntimeInformation.OSDescription
                : productName.Trim(),
            displayVersion,
            build,
            "Windows NT",
            Environment.OSVersion.Version.ToString());

        return new WindowsDetails(osInfo, deviceModel, biosVersion);
    }

    private static string? ReadString(RegistryKey? key, string name)
    {
        if (key is null) return null;
        var value = key.GetValue(name);
        var text = value switch
        {
            string[] lines => string.Join(
                ' ',
                lines.Select(line => line?.Trim()).Where(line => !string.IsNullOrWhiteSpace(line))),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static string? CombineParts(string? left, string? right)
    {
        var trimmedLeft = left?.Trim();
        var trimmedRight = right?.Trim();

        return (trimmedLeft, trimmedRight) switch
        {
            (null or "", null or "")
                => null,
            (null or "", { } r)
                => r,
            ({ } l, null or "")
                => l,
            ({ } l, { } r) when string.Equals(l, r, StringComparison.OrdinalIgnoreCase)
                => l,
            ({ } l, { } r)
                => $"{l} {r}"
        };
    }

    private sealed record WindowsDetails(
        OperatingSystemInfo OperatingSystem,
        string? DeviceModel,
        string? BiosVersion);
}