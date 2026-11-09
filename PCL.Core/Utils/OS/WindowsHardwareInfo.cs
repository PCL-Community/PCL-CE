using System;
using System.Collections.Generic;
using System.Management;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.Utils.OS;

/// <summary>Windows 硬件信息采集实现。</summary>
public sealed class WindowsHardwareInfo : IHardwareInfo
{
    public string? GetCpuName()
    {
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

        return cpuName;
    }

    public IReadOnlyList<HardwareInfo.GpuInfo> GetGpus()
    {
        var gpuList = new List<HardwareInfo.GpuInfo>();
        try
        {
            using var searcher =
                new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_VideoController");
            foreach (var o in searcher.Get())
            {
                var queryObj = (ManagementObject)o;
                gpuList.Add(
                    new HardwareInfo.GpuInfo(
                        queryObj["Name"]?.ToString() ?? "",
                        queryObj["DriverVersion"]?.ToString() ?? "",
                        queryObj["AdapterRAM"] is not null and not DBNull
                            ? Convert.ToInt64(queryObj["AdapterRAM"]) / (1024 * 1024)
                            : 0));
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取 GPU 信息时出错");
        }

        return gpuList;
    }

    public long GetMemorySizeMiB()
    {
        return (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;
    }
}