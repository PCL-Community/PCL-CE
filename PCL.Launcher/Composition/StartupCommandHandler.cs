using Microsoft.Extensions.DependencyInjection;
using PCL.Core.Platform;

namespace PCL;

/// <summary>启动阶段的短命令处理。此类必须在单例服务和 Avalonia 初始化之前调用， 仅处理无需界面的命令行操作。</summary>
internal static class StartupCommandHandler
{
    /// <summary>处理 GPU 偏好命令；若命令已处理则不会返回。</summary>
    /// <returns>没有需要处理的命令时返回 <see langword="false" />。</returns>
    public static bool TryHandle(string[] args, IServiceProvider services)
    {
        if (!GpuPreferenceCommand.IsRequested(args))
            return false;

        var service = services.GetRequiredService<IGpuPreferenceService>();
        var exitCode = GpuPreferenceCommand.Execute(args, service);
        Environment.Exit(exitCode);
        return true;
    }
}
