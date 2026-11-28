using System.Diagnostics.CodeAnalysis;
using System.Management;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

/// <summary>Windows 硬件信息采集实现。</summary>
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public sealed class WindowsHardwareInfo : IHardwareInfo
{
    private readonly Lock _cpuLock = new();
    private bool _cpuLoaded;
    private string? _cpuName;
    private int? _physicalCoreCount;

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
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT * FROM Win32_VideoController");
            foreach (var o in searcher.Get())
            {
                var queryObj = (ManagementObject)o;
                var memory = queryObj["AdapterRAM"] is not null and not DBNull
                    ? Convert.ToInt64(queryObj["AdapterRAM"]) / (1024 * 1024)
                    : 0L;
                gpuList.Add(
                    new HardwareInfo.GpuInfo(
                        queryObj["Name"]?.ToString() ?? "",
                        queryObj["DriverVersion"]?.ToString() ?? "",
                        memory,
                        null,
                        memory >= 4095L));
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 GPU 信息时出错");
        }

        return gpuList;
    }

    private void EnsureCpuInfo()
    {
        lock (_cpuLock)
        {
            if (_cpuLoaded) return;
        }

        string? cpuName = null;
        int? coreCount = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2",
                "SELECT Name, NumberOfCores FROM Win32_Processor");
            var cores = 0;
            foreach (var o in searcher.Get())
            {
                var queryObj = (ManagementObject)o;
                cpuName ??= queryObj["Name"]?.ToString()?.Trim();
                if (queryObj["NumberOfCores"] is not null and not DBNull)
                    cores += Convert.ToInt32(queryObj["NumberOfCores"]);
            }

            if (cores > 0) coreCount = cores;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 CPU 信息时出错");
        }

        lock (_cpuLock)
        {
            _cpuName = cpuName;
            _physicalCoreCount = coreCount;
            _cpuLoaded = true;
        }
    }
}