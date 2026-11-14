using System.Collections.Generic;
using PCL.Core.Utils.OS;

namespace PCL.Core.Platform;

/// <summary>
///     平台硬件信息采集服务。由各平台实现，<see cref="HardwareInfo" /> 只负责汇总状态。
/// </summary>
public interface IHardwareInfo
{
    string? GetCpuName();

    IReadOnlyList<HardwareInfo.GpuInfo> GetGpus();

    long GetMemorySizeMiB();
}