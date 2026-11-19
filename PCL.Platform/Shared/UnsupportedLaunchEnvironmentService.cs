using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>不使用进程环境变量配置 GPU 或渲染器的平台。</summary>
public sealed class UnsupportedLaunchEnvironmentService : ILaunchEnvironmentService
{
    public bool IsSupported => false;

    public void ApplyGpuPreference(ProcessStartInfo startInfo, bool enabled, Action<string> log)
    {
    }

    public void ApplyRenderer(ProcessStartInfo startInfo, int renderer, Action<string> log)
    {
    }
}
