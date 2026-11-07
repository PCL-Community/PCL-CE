using PCL.Core.Platform;

namespace PCL.Core.UI;

public sealed class MacOsSystemFacts : ISystemFacts
{
    public ulong GetPhysicalMemoryBytes()
    {
        return (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    public (ulong Total, ulong Available) GetMemoryBytes()
    {
        return ((ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes);
    }

    public bool IsAdministrator => Environment.IsPrivilegedProcess;
}