namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     崩溃分析请求。该模型只描述日志来源和运行上下文，不包含任何 UI 状态。
/// </summary>
public sealed record CrashAnalysisRequest
{
    public CrashAnalysisSource Source { get; init; }
    public CrashAnalysisMode Mode { get; init; }

    public string? InstancePath { get; init; }
    public string? MinecraftRootPath { get; init; }
    public string? ImportedFilePath { get; init; }
    public string TempDirectory { get; init; } = string.Empty;

    public DateTimeOffset Now { get; init; } = DateTimeOffset.Now;

    public IReadOnlyList<string> CapturedOutputLines { get; init; } = [];
    public string? LaunchScript { get; init; }

    public CrashRuntimeContext RuntimeContext { get; init; } = CrashRuntimeContext.Empty;
}

public enum CrashAnalysisSource
{
    LiveGame,
    ImportedFile
}

public enum CrashAnalysisMode
{
    Automatic,
    Manual
}

/// <summary>
///     启动器层采集到的运行环境。Core 只消费 DTO，不直接读取硬件、账号或 WPF 全局对象。
/// </summary>
public sealed record CrashRuntimeContext
{
    public static CrashRuntimeContext Empty { get; } = new();

    public string? LauncherVersion { get; init; }
    public string? LauncherId { get; init; }
    public string? InstanceName { get; init; }
    public string? InstancePath { get; init; }
    public string? MinecraftVersion { get; init; }
    public string? LoaderName { get; init; }
    public string? JavaInfo { get; init; }
    public string? JavaPath { get; init; }
    public string? AllocatedMemory { get; init; }
    public string? AccountName { get; init; }
    public string? AuthType { get; init; }
    public string? OperatingSystem { get; init; }
    public bool? Is32BitSystem { get; init; }
    public bool? IsArm64System { get; init; }
    public string? CpuName { get; init; }
    public long? SystemMemoryMb { get; init; }
    public IReadOnlyList<CrashGpuInfo> Gpus { get; init; } = [];
    public IReadOnlyList<string> LaunchArguments { get; init; } = [];
}

public sealed record CrashGpuInfo
{
    public string? Name { get; init; }
    public long? MemoryMb { get; init; }
    public string? DriverVersion { get; init; }
}