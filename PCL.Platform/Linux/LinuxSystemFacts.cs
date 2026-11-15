using PCL.Core.Platform;

namespace PCL.Platform;

public sealed class LinuxSystemFacts : ISystemFacts
{
    public SystemMemoryInfo GetMemoryInfo()
    {
        if (LinuxHardwareInfo.TryReadMemory(out var totalMiB, out var availableMiB))
            return new SystemMemoryInfo(
                (ulong)totalMiB * 1024 * 1024,
                (ulong)availableMiB * 1024 * 1024);

        var fallback = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return new SystemMemoryInfo(fallback, fallback);
    }

    public bool IsAdministrator => Environment.IsPrivilegedProcess;
}