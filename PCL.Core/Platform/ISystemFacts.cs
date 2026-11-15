namespace PCL.Core.Platform;

public interface ISystemFacts
{
    bool IsAdministrator { get; }
    SystemMemoryInfo GetMemoryInfo();
}