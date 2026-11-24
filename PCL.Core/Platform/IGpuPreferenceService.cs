namespace PCL.Core.Platform;

public interface IGpuPreferenceService
{
    bool IsSupported { get; }
    void Set(string javaExecutablePath, bool preferHighPerformance);
}