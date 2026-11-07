using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public static class HardwareInfo
{
    private static readonly object _Lock = new();

    /// <summary>
    ///     系统 CPU 信息
    /// </summary>
    public static string CPUName = "Unknown";

    // TODO: macOS 未实现（无 /proc、/sys），CPU 名称保持 Unknown、GPU 为空、内存回退 GC；需补 sysctl/system_profiler 实现。
    private static readonly Lazy<long> _SystemMemorySize = new(_GetSystemMemorySize);

    /// <summary>
    ///     系统 GPU 信息
    /// </summary>
    public static IReadOnlyList<GPUInfo> GPUs { get; private set; } = [];

    /// <summary>
    ///     已安装物理内存大小，单位 MiB。
    /// </summary>
    public static long SystemMemorySize => _SystemMemorySize.Value;

    private static long _GetSystemMemorySize()
    {
        if (OperatingSystem.IsWindows())
            return (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;

        if (TryReadLinuxMemory(out var totalMiB, out _)) return totalMiB;

        var totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return totalBytes > 0 ? totalBytes / 1024 / 1024 : 0;
    }

    /// <summary>读取 Linux <c>/proc/meminfo</c> 的物理内存总量与可用量（MiB）；非 Linux 或读取失败时返回 <c>false</c>。</summary>
    public static bool TryReadLinuxMemory(out long totalMiB, out long availableMiB)
    {
        totalMiB = 0;
        availableMiB = 0;
        if (!OperatingSystem.IsLinux()) return false;

        try
        {
            long? totalKb = null;
            long? availableKb = null;
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    totalKb = _ParseMemInfoValue(line);
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    availableKb = _ParseMemInfoValue(line);
                if (totalKb is not null && availableKb is not null) break;
            }

            if (totalKb is null or 0) return false;
            totalMiB = totalKb.Value / 1024;
            availableMiB = (availableKb ?? 0) / 1024;
            return totalMiB > 0;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "读取 /proc/meminfo 失败");
            return false;
        }
    }

    private static long? _ParseMemInfoValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        if (colonIndex < 0) return null;
        var valuePart = line[(colonIndex + 1)..].Trim();
        var spaceIndex = valuePart.IndexOf(' ');
        if (spaceIndex >= 0) valuePart = valuePart[..spaceIndex];
        return long.TryParse(valuePart, out var value) ? value : null;
    }

    /// <summary>
    ///     获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    /// </summary>
    public static void GetHardwareInfo()
    {
        if (!OperatingSystem.IsWindows())
        {
            _GetLinuxHardwareInfo();
            return;
        }

        // CPU
        string? cpuName = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT * FROM Win32_Processor");
            foreach (var o in searcher.Get())
            {
                var queryObj = (ManagementObject)o;
                cpuName = queryObj["Name"]?.ToString()?.Trim();
                break;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 CPU 信息时出错");
        }

        // GPU
        var gpuList = new List<GPUInfo>();
        try
        {
            using var searcher =
                new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_VideoController");
            foreach (var o in searcher.Get())
            {
                var queryObj = (ManagementObject)o;
                var gpuInfo = new GPUInfo
                {
                    Name = queryObj["Name"]?.ToString() ?? "",
                    DriverVersion = queryObj["DriverVersion"]?.ToString() ?? "",
                    Memory = queryObj["AdapterRAM"] is not null and not DBNull
                        ? Convert.ToInt64(queryObj["AdapterRAM"]) / (1024 * 1024)
                        : 0
                };
                gpuList.Add(gpuInfo);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 GPU 信息时出错");
        }

        lock (_Lock)
        {
            if (cpuName is not null)
                CPUName = cpuName;
            if (gpuList.Count > 0)
                GPUs = gpuList;
        }

        LogWrapper.Info("已获取系统硬件信息");
    }

    private static void _GetLinuxHardwareInfo()
    {
        var cpuName = _TryReadLinuxCpuName();
        var gpuList = _ReadLinuxGpus();

        lock (_Lock)
        {
            if (!string.IsNullOrWhiteSpace(cpuName))
                CPUName = cpuName;
            if (gpuList.Count > 0)
                GPUs = gpuList;
        }

        LogWrapper.Info("已获取系统硬件信息");
    }

    private static string? _TryReadLinuxCpuName()
    {
        if (!OperatingSystem.IsLinux()) return null;

        try
        {
            string? fallback = null;
            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                    return _GetCpuInfoValue(line);
                if (fallback is null
                    && (line.StartsWith("Model", StringComparison.OrdinalIgnoreCase)
                        || line.StartsWith("Hardware", StringComparison.OrdinalIgnoreCase)))
                    fallback = _GetCpuInfoValue(line);
            }

            return fallback;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "读取 /proc/cpuinfo 失败");
            return null;
        }
    }

    private static string _GetCpuInfoValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex < 0 ? string.Empty : line[(colonIndex + 1)..].Trim();
    }

    private static List<GPUInfo> _ReadLinuxGpus()
    {
        var gpuList = new List<GPUInfo>();
        if (!OperatingSystem.IsLinux()) return gpuList;

        const string drmRoot = "/sys/class/drm";
        try
        {
            if (!Directory.Exists(drmRoot)) return gpuList;

            var seenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cardDir in Directory.GetDirectories(drmRoot, "card*"))
            {
                // 只取 cardN，排除 card0-DP-1 之类的显示连接器目录
                var cardName = Path.GetFileName(cardDir);
                if (!cardName.StartsWith("card", StringComparison.Ordinal)
                    || !int.TryParse(cardName[4..], out _)) continue;

                var deviceDir = Path.Combine(cardDir, "device");
                var ueventPath = Path.Combine(deviceDir, "uevent");
                if (!File.Exists(ueventPath)) continue;

                var uevent = _ReadKeyValueFile(ueventPath, '=');
                uevent.TryGetValue("DRIVER", out var driver);
                if (!uevent.TryGetValue("PCI_ID", out var pciId)
                    || string.IsNullOrWhiteSpace(pciId))
                    pciId = _GetPciId(deviceDir);
                if (string.IsNullOrWhiteSpace(pciId)) continue;

                var separatorIndex = pciId.IndexOf(':');
                var vendorId = separatorIndex > 0 ? pciId[..separatorIndex] : string.Empty;
                var deviceId = separatorIndex > 0 ? pciId[(separatorIndex + 1)..] : string.Empty;
                if (!seenDevices.Add($"{vendorId}:{deviceId}")) continue;

                var vendorName = _GetPciVendorName(vendorId);
                var name = vendorName is null
                    ? $"Vendor {vendorId.ToUpperInvariant()} Device {deviceId.ToUpperInvariant()}"
                        .Trim()
                    : $"{vendorName} {deviceId.ToUpperInvariant()}";
                gpuList.Add(
                    new GPUInfo(name, driver ?? string.Empty, _TryReadLinuxVramMiB(deviceDir)));
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "读取 /sys/class/drm 失败");
        }

        return gpuList;
    }

    private static string _GetPciId(string deviceDir)
    {
        var vendor = _ReadTrimmedFile(Path.Combine(deviceDir, "vendor"));
        var device = _ReadTrimmedFile(Path.Combine(deviceDir, "device"));
        if (string.IsNullOrEmpty(vendor) || string.IsNullOrEmpty(device)) return string.Empty;
        return $"{vendor.Replace("0x", "", StringComparison.OrdinalIgnoreCase)}:"
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

    private static long _TryReadLinuxVramMiB(string deviceDir)
    {
        var raw = _ReadTrimmedFile(Path.Combine(deviceDir, "mem_info_vram_total"));
        return long.TryParse(raw, out var bytes) && bytes > 0 ? bytes / 1024 / 1024 : 0;
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

    public readonly record struct GPUInfo(
        string Name,
        string DriverVersion,
        long Memory);
}