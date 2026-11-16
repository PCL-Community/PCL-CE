using System.Diagnostics;
using PCL.Core.Platform;

namespace PCL.Platform;

/// <summary>Linux 通过 Mesa 与 PRIME 环境变量配置游戏进程。</summary>
public sealed class LinuxLaunchEnvironmentService : ILaunchEnvironmentService
{
    public bool IsSupported => true;

    public void ApplyGpuPreference(ProcessStartInfo startInfo, bool enabled, Action<string> log)
    {
        if (!enabled) return;

        startInfo.Environment["DRI_PRIME"] = "1";
        if (!File.Exists("/proc/driver/nvidia/version")) return;

        startInfo.Environment["__NV_PRIME_RENDER_OFFLOAD"] = "1";
        startInfo.Environment["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia";
        startInfo.Environment["__VK_LAYER_NV_optimus"] = "NVIDIA_only";
        log("已通过环境变量请求使用独立显卡（NVIDIA PRIME Render Offload）");
    }

    public void ApplyRenderer(ProcessStartInfo startInfo, int renderer, Action<string> log)
    {
        switch (renderer)
        {
            case 1:
                // llvmpipe：Mesa 软件渲染
                startInfo.Environment["LIBGL_ALWAYS_SOFTWARE"] = "1";
                startInfo.Environment["GALLIUM_DRIVER"] = "llvmpipe";
                break;
            case 2:
                // d3d12 是 Windows 专有的 Mesa 后端，Linux 没有等价实现
                log("d3d12 渲染器在 Linux 上没有等价实现，使用系统默认渲染器");
                break;
            case >= 3:
                // zink：OpenGL over Vulkan
                startInfo.Environment["MESA_LOADER_DRIVER_OVERRIDE"] = "zink";
                startInfo.Environment["GALLIUM_DRIVER"] = "zink";
                break;
        }
    }
}
