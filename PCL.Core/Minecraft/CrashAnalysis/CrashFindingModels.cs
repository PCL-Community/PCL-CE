using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.CrashAnalysis;

public sealed record CrashFinding
{
    public required CrashReasonCode Reason { get; init; }
    public required string RuleId { get; init; }

    public CrashFindingSeverity Severity { get; init; } = CrashFindingSeverity.Error;
    public CrashFindingConfidence Confidence { get; init; } = CrashFindingConfidence.High;

    public IReadOnlyList<CrashFindingParameter> Parameters { get; init; } = [];
    public IReadOnlyList<CrashFindingEvidence> Evidence { get; init; } = [];

    public string? GetParameter(string name)
    {
        return Parameters.FirstOrDefault(parameter => parameter.Name == name)?.Value;
    }
}

public sealed record CrashFindingParameter(string Name, string Value);

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

public sealed record CrashGpuInfo
{
    public string? Name { get; init; }
    public long? MemoryMb { get; init; }
    public string? DriverVersion { get; init; }
}