using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public static class SystemInfo
{
    private static readonly object _lock = new();

    /// <summary>
    /// CPU 名称
    /// </summary>
    public static string CPUName { get; private set; } = null!;

    /// <summary>
    /// 系统 GPU 信息
    /// </summary>
    public static List<GPUInfo> GPUs { get; } = [];

    /// <summary>
    /// 已安装物理内存大小，单位 MB
    /// </summary>
    public static long SystemMemorySize { get; } = (long)KernelInterop.GetPhysicalMemoryBytes().Total / (1024 * 1024);

    /// <summary>
    /// 系统信息描述，例如 Microsoft Windows 11 专业工作站版 10.0.22635.0
    /// </summary>
    public static string OSInfo { get; } = $"{RuntimeInformation.OSDescription} {Environment.OSVersion.Version}";

    public sealed class GPUInfo
    {
        public string Name { get; init; } = null!;
        public string DriverVersion { get; init; } = null!;
        public long Memory { get; init; }
    }

    /// <summary>
    /// 获取 CPU 信息
    /// </summary>
    public static void GetCPUInfo()
    {
        lock (_lock)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_Processor");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    CPUName = queryObj["Name"].ToString().Trim();
                    break; // 通常只需要第一个CPU的信息
                }
                LogWrapper.Info("已获取 CPU 信息");
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "获取 CPU 信息时出错");
            }
        }
    }

    /// <summary>
    /// 获取 GPU 信息
    /// </summary>
    public static void GetGPUInfo()
    {
        lock (_lock)
        {
            try
            {
                GPUs.Clear();
                using var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_VideoController");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    GPUs.Add(new GPUInfo
                    {
                        Name = queryObj["Name"]?.ToString() ?? "",
                        Memory = queryObj["AdapterRAM"] is not null and not DBNull
                            ? Convert.ToInt64(queryObj["AdapterRAM"]) / (1024 * 1024)
                            : 0,
                        DriverVersion = queryObj["DriverVersion"]?.ToString() ?? "",
                    });
                }
                LogWrapper.Info("已获取 GPU 信息");
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "获取 GPU 信息时出错");
            }
        }
    }
}