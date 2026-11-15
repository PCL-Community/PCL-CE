using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

public sealed class MacOsSystemFacts : ISystemFacts
{
    public SystemMemoryInfo GetMemoryInfo()
    {
        var (total, available) = KernelInterop.GetPhysicalMemoryBytes();
        return new SystemMemoryInfo(total, available);
    }

    public bool IsAdministrator => Environment.IsPrivilegedProcess;
}