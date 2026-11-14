using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>当前平台没有可用的 GPU 偏好实现。</summary>
public sealed class UnsupportedGpuPreferenceService : IGpuPreferenceService
{
    public bool IsSupported => false;

    public void Set(string javaExecutablePath, bool preferHighPerformance)
    {
    }
}