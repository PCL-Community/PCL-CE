using PCL.Core.Platform;
using PCL.Core.Utils.OS;

namespace PCL.Platform;

public sealed class WindowsSystemFacts : ISystemFacts
{
    public SystemMemoryInfo GetMemoryInfo()
    {
        var (total, available) = KernelInterop.GetPhysicalMemoryBytes();
        return new SystemMemoryInfo(total, available);
    }

    public bool IsAdministrator => ProcessInterop.IsAdmin();
}