using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Core.UI;

public sealed class WindowsSystemFacts : ISystemFacts
{
    public ulong GetPhysicalMemoryBytes()
    {
        return KernelInterop.GetPhysicalMemoryBytes().Total;
    }

    public (ulong Total, ulong Available) GetMemoryBytes()
    {
        return KernelInterop.GetPhysicalMemoryBytes();
    }

    public bool IsAdministrator => ProcessInterop.IsAdmin();
}