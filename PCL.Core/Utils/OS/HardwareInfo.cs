using System;
using System.Collections.Generic;
using System.Management;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public static class HardwareInfo
{
    private static readonly object _lock = new();
    
    /// <summary>
    /// 系统 CPU 信息
    /// </summary>
    public static string CPUName = "Unknown";

    /// <summary>
    /// 系统 GPU 信息
    /// </summary>
    public static IReadOnlyList<GPUInfo> GPUs { get; private set; } = [];

    /// <summary>
    /// 已安装物理内存大小，单位 MB
    /// </summary>
    public static long SystemMemorySize = (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;

    public readonly record struct GPUInfo(string Name, string DriverVersion, long Memory);

    /// <summary>
    /// 获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    /// </summary>
    public static void GetHardwareInfo()
    {
        // CPU
        var cpuName = (string?)null;
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (ManagementObject queryObj in searcher.Get())
            {
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
            foreach (ManagementObject queryObj in searcher.Get())
            {
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

        lock (_lock)
        {
            if (cpuName is not null)
                CPUName = cpuName;
            if (gpuList.Count > 0)
                GPUs = gpuList;
        }
        LogWrapper.Info("已获取系统硬件信息");
    }
}