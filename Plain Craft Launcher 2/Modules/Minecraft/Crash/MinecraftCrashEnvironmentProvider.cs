using System.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

/// <summary>
///     <p>从启动器运行环境采集错误报告需要的环境信息。</p>
///     <p>
///         这些信息依赖启动器全局状态和硬件检测，所以留在 WPF 项目中。Core 只消费 DTO，
///         不直接读取 <c>SystemInfo</c>、<c>HardwareInfo</c>、<c>Identify</c> 或日志文件。
///     </p>
/// </summary>
public sealed class MinecraftCrashEnvironmentProvider
{
    /// <summary>
    ///     创建错误报告环境信息 DTO。
    /// </summary>
    public static CrashEnvironmentInfo Create(ModMinecraft.Instance? instance, string launchScript)
    {
        return new CrashEnvironmentInfo
        {
            LauncherVersion = ModBase.versionBaseName,
            LauncherId = Identify.LauncherId,
            JavaInfo = _ReadLaunchLogValue("Java 信息："),
            MinecraftFolder = instance?.PathIndie ?? _ReadLaunchLogValue("MC 文件夹："),
            AllocatedMemory = _ReadLaunchLogValue("分配的内存："),
            Log4JNoLookups =
                !launchScript.Contains("-Dlog4j2.formatMsgNoLookups=false", StringComparison.OrdinalIgnoreCase),
            AccountName = _ReadLaunchLogValue("玩家用户名："),
            AuthType = _ReadLaunchLogValue("验证方式："),
            OperatingSystem = SystemInfo.OSInfo,
            Is32BitSystem = SystemInfo.Is32BitSystem,
            IsArm64System = SystemInfo.IsArm64System,
            CpuName = HardwareInfo.CPUName,
            SystemMemoryMb = HardwareInfo.SystemMemorySize,
            Gpus = HardwareInfo.GPUs.Select(static gpu => new CrashGpuInfo
            {
                Name = gpu.Name,
                MemoryMb = gpu.Memory,
                DriverVersion = gpu.DriverVersion
            }).ToList()
        };
    }

    private static string? _ReadLaunchLogValue(string key)
    {
        try
        {
            var logFile = LogWrapper.CurrentLogger.CurrentLogFiles.LastOrDefault();
            if (string.IsNullOrWhiteSpace(logFile) || !File.Exists(logFile)) return null;

            var text = ModBase.ReadFile(logFile);
            var start = text.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += key.Length;
            var end = text.IndexOf('[', start);
            var value = end < 0 ? text[start..] : text[start..end];
            return value.Trim();
        }
        catch
        {
            return null;
        }
    }
}