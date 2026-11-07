namespace PCL.Core.Platform;

public interface ISystemFacts
{
    bool IsAdministrator { get; }
    ulong GetPhysicalMemoryBytes();
    (ulong Total, ulong Available) GetMemoryBytes();
}