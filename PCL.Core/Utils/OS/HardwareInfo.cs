using System;
using System.Collections.Generic;
using System.Threading;
using PCL.Core.Logging;
using PCL.Core.Platform;

namespace PCL.Core.Utils.OS;

/// <summary>
///     系统硬件信息门面。具体采集由当前平台的 <see cref="IHardwareInfo" /> 实现完成。
/// </summary>
public static class HardwareInfo
{
    private static readonly Lock _Lock = new();

    /// <summary>
    ///     系统 CPU 信息。
    /// </summary>
    public static string CpuName = "Unknown";

    private static readonly Lazy<long> _SystemMemorySize = new(_GetSystemMemorySize);

    /// <summary>
    ///     系统 GPU 信息。
    /// </summary>
    public static IReadOnlyList<GpuInfo> Gpus { get; private set; } = [];

    /// <summary>
    ///     已安装物理内存大小，单位 MiB。
    /// </summary>
    public static long SystemMemorySize => _SystemMemorySize.Value;

    private static IHardwareInfo? CurrentProvider =>
        PlatformRuntime.IsInitialized ? PlatformRuntime.Current.Hardware : null;

    private static long _GetSystemMemorySize()
    {
        if (CurrentProvider is { } provider)
            try
            {
                var size = provider.GetMemorySizeMiB();
                if (size > 0) return size;
            }
            catch (Exception ex)
            {
                LogWrapper.Warn(ex, "获取系统内存信息失败");
            }

        var totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return totalBytes > 0 ? totalBytes / 1024 / 1024 : 0;
    }

    /// <summary>
    ///     获取系统信息，例如 CPU 与 GPU，并存储到 <see cref="CpuName" /> 和 <see cref="Gpus" />。
    /// </summary>
    public static void GetHardwareInfo()
    {
        if (CurrentProvider is not { } provider)
        {
            LogWrapper.Debug("Hardware", "平台模块尚未初始化，跳过硬件信息采集");
            return;
        }

        try
        {
            var cpuName = provider.GetCpuName();
            var gpuList = provider.GetGpus();

            lock (_Lock)
            {
                if (!string.IsNullOrWhiteSpace(cpuName))
                    CpuName = cpuName;
                if (gpuList.Count > 0)
                    Gpus = gpuList;
            }

            LogWrapper.Info("已获取系统硬件信息");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "获取系统硬件信息失败");
        }
    }

    public readonly record struct GpuInfo(
        string Name,
        string DriverVersion,
        long Memory);
}