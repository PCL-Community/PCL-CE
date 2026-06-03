using System.IO;
using PCL.Core.Logging;
using PCL.Core.Minecraft.CrashAnalysis;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL;

public static class MinecraftCrashEnvironmentProvider
{
    public static CrashRuntimeContext Create(ModMinecraft.Instance? instance, string? launchScript)
    {
        var info = instance?.Info;
        return new CrashRuntimeContext
        {
            LauncherVersion = ModBase.versionBaseName,
            LauncherId = Identify.LauncherId,
            InstanceName = instance?.Name,
            InstancePath = instance?.PathInstance,
            MinecraftVersion = info?.VanillaName,
            LoaderName = _GetLoaderName(info),
            JavaInfo = _ReadLaunchLogValue("Java 信息："),
            JavaPath = _ReadLaunchLogValue("Java 路径："),
            AllocatedMemory = _ReadLaunchLogValue("分配的内存："),
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
            }).ToList(),
            LaunchArguments = string.IsNullOrWhiteSpace(launchScript)
                ? []
                : launchScript.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(static item => item.StartsWith("-", StringComparison.Ordinal)).Take(40).ToList()
        };
    }

    private static string? _GetLoaderName(ModMinecraft.McInstanceInfo? info)
    {
        if (info is null) return null;
        if (info.HasFabric) return "Fabric " + info.Fabric;
        if (info.HasForge) return "Forge " + info.Forge;
        if (info.HasNeoForge) return "NeoForge " + info.NeoForge;
        if (info.HasQuilt) return "Quilt " + info.Quilt;
        if (info.HasLiteLoader) return "LiteLoader";
        if (info.HasCleanroom) return "Cleanroom " + info.Cleanroom;
        return null;
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
            return (end < 0 ? text[start..] : text[start..end]).Trim();
        }
        catch
        {
            return null;
        }
    }
}