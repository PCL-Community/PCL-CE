using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Core.UI;

public sealed class MacOsSystemFacts : ISystemFacts
{
    public ulong GetPhysicalMemoryBytes()
    {
        return KernelInterop.GetPhysicalMemoryBytes().Total;
    }

    public (ulong Total, ulong Available) GetMemoryBytes()
    {
        return KernelInterop.GetPhysicalMemoryBytes();
    }

    public bool IsAdministrator => Environment.IsPrivilegedProcess;
}