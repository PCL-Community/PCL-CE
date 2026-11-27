using System.ComponentModel;
using System.Diagnostics;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

/// <summary>Linux 硬件信息采集实现。</summary>
public sealed class LinuxHardwareInfo : IHardwareInfo
{
    private const string DrmRoot = "/sys/class/drm";
    private static readonly Lock CpuLock = new();
    private static bool _cpuLoaded;
    private static string? _cpuName;
    private static int? _physicalCoreCount;

    public string? GetCpuName()
    {
        EnsureCpuInfo();
        return _cpuName;
    }

    public int? GetPhysicalCoreCount()
    {
        EnsureCpuInfo();
        return _physicalCoreCount;
    }

    public IReadOnlyList<HardwareInfo.GpuInfo> GetGpus()
    {
        var gpuList = new List<HardwareInfo.GpuInfo>();
        var nvidiaGpus = QueryNvidiaSmi();
        try
        {
            if (!Directory.Exists(DrmRoot))
                return gpuList;

            var seenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cardDir in Directory.GetDirectories(DrmRoot, "card*"))
            {
                var cardName = Path.GetFileName(cardDir);
                if (!cardName.StartsWith("card", StringComparison.Ordinal)
                    || !int.TryParse(cardName[4..], out _))
                    continue;

                var deviceDir = Path.Combine(cardDir, "device");
                var ueventPath = Path.Combine(deviceDir, "uevent");
                if (!File.Exists(ueventPath))
                    continue;

                var uevent = _ReadKeyValueFile(ueventPath, '=');
                uevent.TryGetValue("DRIVER", out var driver);
                if (!uevent.TryGetValue("PCI_ID", out var pciId)
                    || string.IsNullOrWhiteSpace(pciId))
                    pciId = _GetPciId(deviceDir);
                if (string.IsNullOrWhiteSpace(pciId))
                    continue;

                var separatorIndex = pciId.IndexOf(':');
                var vendorId = separatorIndex > 0
                    ? pciId[..separatorIndex]
                    : string.Empty;
                var deviceId = separatorIndex > 0
                    ? pciId[(separatorIndex + 1)..]
                    : string.Empty;

                uevent.TryGetValue("PCI_SLOT_NAME", out var slot);
                var identity = string.IsNullOrWhiteSpace(slot)
                    ? $"{vendorId}:{deviceId}"
                    : slot;
                if (!seenDevices.Add(identity))
                    continue;

                var nvidia = string.IsNullOrWhiteSpace(slot)
                    ? null
                    : nvidiaGpus.GetValueOrDefault(NormalizeBusId(slot));

                var hasPciName = PciIdsDatabase.TryGetDevice(
                    vendorId,
                    deviceId,
                    out var codename,
                    out var pciModel);
                var vendorName = _GetPciVendorName(vendorId);

                var name = nvidia?.Name;
                if (string.IsNullOrWhiteSpace(name) && IsNvidia(vendorId))
                    name = _GetNvidiaModel(slot);
                if (string.IsNullOrWhiteSpace(name) && hasPciName)
                    name = CombineGpuName(vendorName, pciModel);
                if (string.IsNullOrWhiteSpace(name))
                    name = string.IsNullOrWhiteSpace(vendorName)
                        ? $"Vendor {vendorId.ToUpperInvariant()} Device {deviceId.ToUpperInvariant()}"
                        : $"{vendorName} {deviceId.ToUpperInvariant()}";

                var driverVersion = nvidia?.DriverVersion;
                if (string.IsNullOrWhiteSpace(driverVersion) && IsNvidia(vendorId))
                    driverVersion = _ReadTrimmedFile("/sys/module/nvidia/version");
                if (string.IsNullOrWhiteSpace(driverVersion))
                    driverVersion = driver;

                var memory = nvidia?.MemoryMiB ?? 0L;
                if (memory <= 0)
                    memory = _TryReadLinuxVRamMiB(deviceDir);

                gpuList.Add(
                    new HardwareInfo.GpuInfo(
                        name.Trim(),
                        driverVersion ?? string.Empty,
                        memory,
                        string.IsNullOrWhiteSpace(codename) ? null : codename.Trim()));
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "读取 /sys/class/drm 失败");
        }

        return gpuList;
    }

    private static void EnsureCpuInfo()
    {
        lock (CpuLock)
        {
            if (_cpuLoaded)
                return;
        }

        string? cpuName = null;
        try
        {
            string? fallback = null;
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                {
                    cpuName = _GetCpuInfoValue(line);
                    break;
                }

                if (fallback is null
                    && (line.StartsWith("Model", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("Hardware", StringComparison.OrdinalIgnoreCase)))
                    fallback = _GetCpuInfoValue(line);
            }

            cpuName ??= fallback;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "读取 /proc/cpuinfo 失败");
        }

        var physicalCoreCount = _ReadPhysicalCoreCount();
        lock (CpuLock)
        {
            _cpuName = string.IsNullOrWhiteSpace(cpuName)
                ? null
                : cpuName.Trim();
            _physicalCoreCount = physicalCoreCount is > 0
                ? physicalCoreCount
                : null;
            _cpuLoaded = true;
        }
    }

    private static int? _ReadPhysicalCoreCount()
    {
        try
        {
            var cores = new HashSet<string>(StringComparer.Ordinal);
            const string cpuRoot = "/sys/devices/system/cpu";
            if (Directory.Exists(cpuRoot))
                foreach (var cpuDir in Directory.EnumerateDirectories(cpuRoot, "cpu*"))
                {
                    var name = Path.GetFileName(cpuDir);
                    if (!name.StartsWith("cpu", StringComparison.Ordinal)
                        || !int.TryParse(name[3..], out _))
                        continue;

                    var topology = Path.Combine(cpuDir, "topology");
                    var package = _ReadTrimmedFile(Path.Combine(topology, "physical_package_id"));
                    var core = _ReadTrimmedFile(Path.Combine(topology, "core_id"));
                    if (package is not null && core is not null)
                        cores.Add($"{package}:{core}");
                }

            if (cores.Count > 0)
                return cores.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        try
        {
            var cores = new HashSet<string>(StringComparer.Ordinal);
            string? package = null;
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
                if (line.StartsWith("physical id", StringComparison.OrdinalIgnoreCase))
                    package = _GetCpuInfoValue(line);
                else if (line.StartsWith("core id", StringComparison.OrdinalIgnoreCase)
                         && package is not null)
                    cores.Add($"{package}:{_GetCpuInfoValue(line)}");

            if (cores.Count > 0)
                return cores.Count;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static Dictionary<string, NvidiaGpu> QueryNvidiaSmi()
    {
        var result = new Dictionary<string, NvidiaGpu>(StringComparer.OrdinalIgnoreCase);
        var output = _RunAndRead(
            "nvidia-smi",
            "--query-gpu=name,memory.total,driver_version,pci.bus_id --format=csv,noheader,nounits",
            2000);
        if (string.IsNullOrWhiteSpace(output))
            return result;

        foreach (var line in output.Split('\n'))
        {
            var parts = line.Split(',', 4);
            if (parts.Length < 4)
                continue;

            var name = parts[0].Trim();
            if (name.Length == 0)
                continue;

            _ = long.TryParse(parts[1].Trim(), out var memory);
            var driver = parts[2].Trim();
            var busId = NormalizeBusId(parts[3].Trim());
            result[busId] = new NvidiaGpu(name, memory, driver);
        }

        return result;
    }

    private static string? _GetNvidiaModel(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return null;
        var infoPath = $"/proc/driver/nvidia/gpus/{slot}/information";
        try
        {
            if (!File.Exists(infoPath)) return null;
            foreach (var line in File.ReadLines(infoPath))
            {
                if (!line.StartsWith("Model:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = line["Model:".Length..].Trim();
                if (value.Length == 0)
                    continue;

                return value.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase)
                    ? value
                    : $"NVIDIA {value}";
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    private static string _GetCpuInfoValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex < 0
            ? string.Empty
            : line[(colonIndex + 1)..].Trim();
    }

    private static string _GetPciId(string deviceDir)
    {
        var vendor = _ReadTrimmedFile(Path.Combine(deviceDir, "vendor"));
        var device = _ReadTrimmedFile(Path.Combine(deviceDir, "device"));
        return string.IsNullOrEmpty(vendor) || string.IsNullOrEmpty(device)
            ? string.Empty
            : $"{vendor.Replace("0x", "", StringComparison.OrdinalIgnoreCase)}:"
            + $"{device.Replace("0x", "", StringComparison.OrdinalIgnoreCase)}";
    }

    private static string? _GetPciVendorName(string vendorId)
    {
        return vendorId.ToLowerInvariant() switch
        {
            "8086" => "Intel",
            "10de" => "NVIDIA",
            "1002" or "1022" => "AMD",
            "1af4" => "Red Hat Virtio",
            "1b36" or "1234" => "QEMU",
            "1414" => "Microsoft",
            "15ad" => "VMware",
            "80ee" => "VirtualBox",
            _ => null
        };
    }

    private static string CombineGpuName(string? vendorName, string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return string.Empty;
        return string.IsNullOrWhiteSpace(vendorName)
            || model.StartsWith(vendorName, StringComparison.OrdinalIgnoreCase)
                ? model.Trim()
                : $"{vendorName} {model.Trim()}";
    }

    private static bool IsNvidia(string vendorId)
    {
        return string.Equals(vendorId, "10de", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeBusId(string busId)
    {
        var parts = busId.Split(
            ':',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? $"{parts[^2]}:{parts[^1]}" : busId.Trim();
    }

    private static long _TryReadLinuxVRamMiB(string deviceDir)
    {
        var raw = _ReadTrimmedFile(Path.Combine(deviceDir, "mem_info_vram_total"));
        return long.TryParse(raw, out var bytes) && bytes > 0
            ? bytes / 1024 / 1024
            : 0;
    }

    private static string? _ReadTrimmedFile(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> _ReadKeyValueFile(string path, char separator)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            var separatorIndex = line.IndexOf(separator);
            if (separatorIndex <= 0) continue;
            result[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
        }

        return result;
    }

    private static string _RunAndRead(string fileName, string arguments, int timeoutMilliseconds)
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
            if (process is null) return string.Empty;

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMilliseconds))
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
            return outputTask.GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private sealed record NvidiaGpu(
        string Name,
        long MemoryMiB,
        string DriverVersion);
}