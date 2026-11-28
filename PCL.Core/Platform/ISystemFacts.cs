namespace PCL.Core.Platform;

public interface ISystemFacts
{
    bool IsAdministrator { get; }

    SystemMemoryInfo GetMemoryInfo();

    OperatingSystemInfo GetOperatingSystemInfo();

    string? GetDeviceModel();

    string? GetBiosVersion();
}