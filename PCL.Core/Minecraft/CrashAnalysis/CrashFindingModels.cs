using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.CrashAnalysis;

/// <summary>
///     <p>规则系统输出的单条结构化崩溃发现。</p>
///     <p>
///         这里保存的是“发生了什么”，而不是“如何告诉用户”。所有用户可见文案必须走语言文件。
///         <see cref="RuleId" /> 用于测试和调试，<see cref="Reason" /> 用于本地化 key 选择，
///         <see cref="Parameters" /> 用于填充 <c>{0}</c>、<c>{1}</c> 等占位符。
///     </p>
/// </summary>
public sealed record CrashFinding
{
    public required CrashReasonCode Reason { get; init; }
    public required string RuleId { get; init; }

    public CrashFindingSeverity Severity { get; init; } = CrashFindingSeverity.Error;
    public CrashFindingConfidence Confidence { get; init; } = CrashFindingConfidence.High;

    public IReadOnlyList<CrashFindingParameter> Parameters { get; init; } = [];
    public IReadOnlyList<CrashFindingEvidence> Evidence { get; init; } = [];

    /// <summary>
    ///     按参数名读取规则提取出的结构化值。
    /// </summary>
    public string? GetParameter(string name)
    {
        return Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value;
    }
}

/// <summary>
///     规则提取出的参数，例如 Mod 名称、方块 ID、实体名或加载器提供的详细错误。
/// </summary>
public sealed record CrashFindingParameter(string Name, string Value);

/// <summary>
///     <p>所有规则和本地化器共享的参数名常量。</p>
///     <p>
///         参数名必须稳定，因为它们连接了规则、<see cref="CrashResultLocalizer" /> 和测试。
///         新增参数时优先在这里声明，避免不同规则写出大小写不一致的字符串。
///     </p>
/// </summary>
public static class CrashFindingParameterNames
{
    public const string Detail = "Detail";
    public const string ModName = "ModName";
    public const string ModNames = "ModNames";
    public const string Keyword = "Keyword";
    public const string Keywords = "Keywords";
    public const string BlockName = "BlockName";
    public const string EntityName = "EntityName";
    public const string FileName = "FileName";
    public const string RequiresModLoaderChange = "RequiresModLoaderChange";
}

/// <summary>
///     规则命中时保留的证据，主要用于调试和未来的详细报告，不直接展示给普通用户。
/// </summary>
public sealed record CrashFindingEvidence
{
    public required CrashLogKind Source { get; init; }
    public string? MatchedText { get; init; }
    public int? LineNumber { get; init; }
}

public enum CrashFindingSeverity
{
    Info,
    Warning,
    Error
}

public enum CrashFindingConfidence
{
    Low,
    Medium,
    High
}

/// <summary>
///     <p>稳定的崩溃原因代码。</p>
///     <p>
///         枚举名必须使用英文并保持语义稳定；它们会映射到 <c>Crash.Finding.{Reason}</c> i18n key。
///         不要在枚举名中使用中文，也不要把用户展示语句塞入枚举或规则。
///     </p>
/// </summary>
public enum CrashReasonCode
{
    NoAnalyzableLog,

    OutOfMemory,
    ThirtyTwoBitJavaMemoryLimit,

    UsingJdk,
    UsingOpenJ9,
    JavaVersionTooHigh,
    JavaVersionIncompatible,
    ModRequiresJava11,

    UnsupportedOpenGl,
    PixelFormatUnsupported,
    IntelDriverAccessViolation,
    AmdDriverAccessViolation,
    NvidiaDriverAccessViolation,
    ResourcePackTooLargeOrGpuInsufficient,
    ShaderOrResourcePackOpenGl1282,

    ExtractedModJar,
    InvalidModFileName,
    DuplicateModInstalled,
    IncompatibleMods,
    MissingDependencyOrWrongMinecraftVersion,

    MissingMixinBootstrap,
    ModMixinFailed,
    ModInitializationFailed,
    ConfirmedModCrash,
    SuspectedModCrash,
    ModConfigCrash,

    FabricError,
    FabricProvidedSolution,
    ForgeError,
    ModLoaderError,

    OptiFineForgeIncompatible,
    OptiFineWorldLoadCrash,
    ShadersModWithOptiFine,

    IncompleteForgeInstallation,
    MultipleForgeArguments,
    OldForgeHighJavaIncompatible,
    TooManyModsIdLimit,
    NightConfigBug,

    StackTraceKeyword,
    StackTraceModName,

    SpecificBlockCrash,
    SpecificEntityCrash,

    ManualDebugCrash,
    VeryShortProgramOutput,
    FileIntegrityFailed
}

/// <summary>
///     <p>导出错误报告时使用的运行环境信息。</p>
///     <p>
///         Core 只消费这个 DTO，不主动读取 <c>HardwareInfo</c>、<c>SystemInfo</c>、账号对象或启动器全局状态。
///         这些信息由 UI 层的 <c>MinecraftCrashEnvironmentProvider</c> 负责采集。
///     </p>
/// </summary>
public sealed record CrashEnvironmentInfo
{
    public string? LauncherVersion { get; init; }
    public string? LauncherId { get; init; }

    public string? AccountName { get; init; }
    public string? AuthType { get; init; }

    public string? JavaInfo { get; init; }
    public string? MinecraftFolder { get; init; }
    public string? AllocatedMemory { get; init; }
    public bool? Log4JNoLookups { get; init; }

    public string? OperatingSystem { get; init; }
    public bool? Is32BitSystem { get; init; }
    public bool? IsArm64System { get; init; }

    public string? CpuName { get; init; }
    public long? SystemMemoryMb { get; init; }

    public IReadOnlyList<CrashGpuInfo> Gpus { get; init; } = [];
}

/// <summary>
///     导出环境报告时使用的显卡信息。
/// </summary>
public sealed record CrashGpuInfo
{
    public string? Name { get; init; }
    public long? MemoryMb { get; init; }
    public string? DriverVersion { get; init; }
}