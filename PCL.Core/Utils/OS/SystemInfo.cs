using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

public static class SystemInfo
{
    private static readonly object _lock = new();

    public static string CPUName = null!;

    /// <summary>
    /// 系统 GPU 信息
    /// </summary>
    public static List<GPUInfo> GPUs = new();

    /// <summary>
    /// 已安装物理内存大小，单位 MB
    /// </summary>
    public static long SystemMemorySize = (long)KernelInterop.GetPhysicalMemoryBytes().Total / 1024 / 1024;

    /// <summary>
    /// 系统信息描述，例如 Microsoft Windows 11 专业工作站版 10.0.22635.0
    /// </summary>
    public static string OSInfo = RuntimeInformation.OSDescription + " " + Environment.OSVersion.Version;

    public class GPUInfo
    {
        public string Name = null!;
      
        public string DriverVersion = null!;

        /// <summary>
        /// 显存大小，单位 MB
        /// </summary>
        public long Memory;
    }

    /// <summary>
    /// 获取系统信息，例如 CPU 与 GPU，并存储到 CPUName 和 GPUs
    /// </summary>
    public static void GetSystemInfo()
    {
        lock (_lock)
        {
            // CPU
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_Processor");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    CPUName = queryObj["Name"].ToString().Trim();
                    break; // 通常只需要第一个CPU的信息
                }
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "获取 CPU 信息时出错");
            }

            // GPU
            try
            {
                GPUs.Clear();
                using var searcher = new ManagementObjectSearcher(@"root\CIMV2", "SELECT * FROM Win32_VideoController");
                foreach (ManagementObject queryObj in searcher.Get())
                {
                    var gpuInfo = new GPUInfo();

                    if (queryObj["Name"] is not null)
                        gpuInfo.Name = queryObj["Name"].ToString();
                    if (queryObj["AdapterRAM"] is not null and not DBNull)
                        gpuInfo.Memory = Convert.ToInt64(queryObj["AdapterRAM"]) / (1024 * 1024);
                    if (queryObj["DriverVersion"] is not null)
                        gpuInfo.DriverVersion = queryObj["DriverVersion"].ToString();

                    GPUs.Add(gpuInfo);
                }
                LogWrapper.Info("已获取系统环境信息");
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "获取 GPU 信息时出错");
            }
        }
    }
}