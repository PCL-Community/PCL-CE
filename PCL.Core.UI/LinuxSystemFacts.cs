using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Core.UI;

public sealed class LinuxSystemFacts : ISystemFacts
{
    public ulong GetPhysicalMemoryBytes()
    {
        return LinuxHardwareInfo.TryReadMemory(out var total, out _)
            ? (ulong)total * 1024 * 1024
            : (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    public (ulong Total, ulong Available) GetMemoryBytes()
    {
        return LinuxHardwareInfo.TryReadMemory(out var total, out var available)
            ? ((ulong)total * 1024 * 1024, (ulong)available * 1024 * 1024)
            : ((ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
    }

    public bool IsAdministrator => Environment.IsPrivilegedProcess;
}