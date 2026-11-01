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
    public static string CPUName = "Unknown";

    /// <summary>
    /// 系统 GPU 信息
    /// </summary>
    public static IReadOnlyList<GPUInfo> GPUs { get; private set; } = [];

    private static readonly Lazy<long> _SystemMemorySize = new(_GetSystemMemorySize);

    /// <summary>
    /// 已安装物理内存大小，单位 MiB。非 Windows 平台以 GC 报告的本机可用内存作为近似值。
    /// </summary>
    public static long SystemMemorySize => _SystemMemorySize.Value;

    private static long _GetSystemMemorySize()
    {
        if (OperatingSystem.IsWindows())
            return (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;

        var totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return totalBytes > 0 ? totalBytes / 1024 / 1024 : 0;
    }

    public readonly record struct GPUInfo(
        string Name,
        string DriverVersion,
        long Memory);

    /// <summary>
    /// 获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    /// </summary>
    public static void GetHardwareInfo()
    {
        if (!OperatingSystem.IsWindows())
        {
            // System.Management 的 WMI 查询仅在 Windows 上可用
            LogWrapper.Info("当前平台不支持 WMI，已跳过系统硬件信息获取");
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
}