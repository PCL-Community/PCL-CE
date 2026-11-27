using System.ComponentModel;
using System.Diagnostics;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

public sealed class MacOsSystemFacts : ISystemFacts
{
    private static readonly Lazy<MacDetails> LazyDetails = new(ReadDetails);

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

    private static MacDetails ReadDetails()
    {
        var productName = RunAndRead("sw_vers", "-productName");
        var productVersion = RunAndRead("sw_vers", "-productVersion");
        var build = RunAndRead("sw_vers", "-buildVersion");
        var displayName = string.IsNullOrWhiteSpace(productName)
            ? "macOS"
            : productName;
        if (!string.IsNullOrWhiteSpace(productVersion)
            && !displayName.Contains(productVersion, StringComparison.Ordinal))
            displayName = $"{displayName} {productVersion}";

        var kernelVersion = RunAndRead("sysctl", "-n kern.osrelease");
        if (string.IsNullOrWhiteSpace(kernelVersion))
            kernelVersion = Environment.OSVersion.Version.ToString();

        var deviceModel = RunAndRead("sysctl", "-n hw.model");
        return new MacDetails(
            new OperatingSystemInfo(
                displayName.Trim(),
                string.IsNullOrWhiteSpace(productVersion) ? null : productVersion,
                string.IsNullOrWhiteSpace(build) ? null : build,
                "Darwin",
                kernelVersion),
            string.IsNullOrWhiteSpace(deviceModel) ? null : deviceModel,
            null);
    }

    private static string RunAndRead(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo(fileName, arguments)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment =
                    {
                        ["LANG"] = "C"
                    }
                });
            if (process is null)
                return string.Empty;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(1500))
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                }

                return string.Empty;
            }

            _ = errorTask.GetAwaiter().GetResult();
            return outputTask.GetAwaiter().GetResult().Trim();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private sealed record MacDetails(
        OperatingSystemInfo OperatingSystem,
        string? DeviceModel,
        string? BiosVersion);
}