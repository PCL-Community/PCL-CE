using System;
using System.Collections.Generic;
using System.Management;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public static class HardwareInfo
{
    private static readonly object _Lock = new();
    
    /// <summary>
    /// 系统 CPU 信息
    /// </summary>
    public static string CpuName = "Unknown";

    /// <summary>
    /// 系统 GPU 信息
    /// </summary>
    public static IReadOnlyList<GpuInfo> GpUs { get; private set; } = [];

    /// <summary>
    /// 已安装物理内存大小，单位 MB
    /// </summary>
    public static readonly long SystemMemorySize = (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;

    public readonly record struct GpuInfo(string Name, string DriverVersion, long Memory);

    /// <summary>
    /// 获取系统信息，例如 CPU 与 GPU，并存储到 CpuName 和 GPUs
    /// </summary>
    public static void GetHardwareInfo()
    {
        // CPU
        var cpuName = (string?)null;
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_Processor");
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
        var gpuList = new List<GpuInfo>();
        try
        {
            using var searcher =
                new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_VideoController");
            foreach (var o in searcher.Get())
            {
                var queryObj = (ManagementObject)o;
                var gpuInfo = new GpuInfo
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
                CpuName = cpuName;
            if (gpuList.Count > 0)
                GpUs = gpuList;
        }
        LogWrapper.Info("已获取系统硬件信息");
    }
}