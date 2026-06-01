using System.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

public sealed class MinecraftCrashEnvironmentProvider
{
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