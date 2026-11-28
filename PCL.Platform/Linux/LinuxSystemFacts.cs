using System.Runtime.InteropServices;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

public sealed class LinuxSystemFacts : ISystemFacts
{
    private static readonly string[] ReleaseFiles = ["/etc/os-release", "/usr/lib/os-release"];
    private static readonly Lazy<LinuxDetails> LazyDetails = new(ReadDetails);

    public SystemMemoryInfo GetMemoryInfo()
    {
        var (total, available) = KernelInterop.GetPhysicalMemoryBytes();
        return new SystemMemoryInfo(total, available);
    }

    public bool IsAdministrator => Environment.IsPrivilegedProcess;

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

    private static LinuxDetails ReadDetails()
    {
        var release = ReadOsRelease();
        var name = GetValue(release, "PRETTY_NAME");
        if (string.IsNullOrWhiteSpace(name))
            name = CombineParts(
                GetValue(release, "NAME"),
                GetValue(release, "VERSION"));
        if (string.IsNullOrWhiteSpace(name))
            name = RuntimeInformation.OSDescription;

        var version = GetValue(release, "VERSION_ID");
        var build = GetValue(release, "BUILD_ID");
        var kernelVersion = ReadTrimmedFile("/proc/sys/kernel/osrelease")
            ?? Environment.OSVersion.Version.ToString();

        var productName = ReadTrimmedFile("/sys/class/dmi/id/product_name");
        var productVersion = ReadTrimmedFile("/sys/class/dmi/id/product_version");
        var hasProductName = IsUsableDmiValue(productName);
        var hasProductVersion = IsUsableDmiValue(productVersion);
        var product = (hasProductName, hasProductVersion) switch
        {
            (true, true) => $"{productName!.Trim()} ({productVersion!.Trim()})",
            (true, false) => productName!.Trim(),
            (false, true) => productVersion!.Trim(),
            (false, false) => null
        };
        var deviceModel = CombineParts(
            ReadTrimmedFile("/sys/class/dmi/id/sys_vendor"),
            product);

        var bios = ReadTrimmedFile("/sys/class/dmi/id/bios_version");
        var biosDate = ReadTrimmedFile("/sys/class/dmi/id/bios_date");
        var biosVersion = string.IsNullOrWhiteSpace(bios)
            ? null
            : string.IsNullOrWhiteSpace(biosDate)
                ? bios
                : $"{bios} ({biosDate})";

        return new LinuxDetails(
            new OperatingSystemInfo(
                name.Trim(),
                version,
                build,
                "Linux",
                kernelVersion),
            deviceModel,
            biosVersion);
    }

    private static Dictionary<string, string> ReadOsRelease()
    {
        foreach (var path in ReleaseFiles)
            try
            {
                if (!File.Exists(path))
                    continue;

                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line[0] == '#')
                        continue;

                    var separator = line.IndexOf('=');
                    if (separator <= 0)
                        continue;

                    var key = line[..separator].Trim();
                    var value = Unquote(line[(separator + 1)..].Trim());
                    if (key.Length != 0) values[key] = value;
                }

                if (values.Count != 0)
                    return values;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }

        return [];
    }

    private static string? GetValue(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static string? ReadTrimmedFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var value = File.ReadAllText(path).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Unquote(string value)
    {
        return value.Length >= 2
            && ((value[0] == '"' && value[^1] == '"')
                || (value[0] == '\'' && value[^1] == '\''))
                ? value[1..^1].Trim()
                : value;
    }

    private static bool IsUsableDmiValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Trim().ToLowerInvariant() switch
            {
                "invalid"
                    or "default string"
                    or "none"
                    or "unknown"
                    or "not specified"
                    or "system product name"
                    or "to be filled by o.e.m." => false,
                _ => true
            };
    }

    private static string? CombineParts(string? left, string? right)
    {
        return (left, right) switch
        {
            var (l, r) when string.IsNullOrWhiteSpace(l)
                => string.IsNullOrWhiteSpace(r) ? null : r!.Trim(),
            var (l, r) when string.IsNullOrWhiteSpace(r)
                => l!.Trim(),
            var (l, r) when r!.Contains(l!.Trim(), StringComparison.OrdinalIgnoreCase)
                => r.Trim(),
            var (l, r)
                => $"{l!.Trim()} {r!.Trim()}"
        };
    }

    private sealed record LinuxDetails(
        OperatingSystemInfo OperatingSystem,
        string? DeviceModel,
        string? BiosVersion);
}