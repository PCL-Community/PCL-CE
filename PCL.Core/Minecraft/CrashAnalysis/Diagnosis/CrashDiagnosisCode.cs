namespace PCL.Core.Minecraft.CrashAnalysis;

public enum CrashDiagnosisCode
{
    Unknown,
    RuntimeMemoryExhausted,
    RuntimeJavaTooOld,
    RuntimeJavaTooNew,
    RuntimeJavaVendorUnsupported,
    RuntimeArchitectureMismatch,
    GraphicsOpenGlUnavailable,
    GraphicsDriverNativeCrash,
    GraphicsResourceOrShaderOverload,
    LoaderDependencyMissing,
    LoaderDependencyVersionConflict,
    LoaderMixinFailure,
    LoaderTransformFailure,
    LoaderInstallationIncomplete,
    ModLikelyCausedCrash,
    ModSetConflict,
    ModDuplicateInstalled,
    ModFileInvalidOrCorrupted,
    ModConfigInvalid,
    GameWorldBlockEntityCorrupted,
    GameWorldEntityCorrupted,
    GameResourcePackFailed,
    GameShaderFailed,
    NativeJvmCrash,
    LauncherCapturedNoUsefulLog,
    AnalysisInconclusive
}

public enum CrashDiagnosisCategory
{
    Runtime,
    Graphics,
    ModLoader,
    Mod,
    GameContent,
    Native,
    Launcher,
    Unknown
}

public enum CrashDiagnosisConfidence
{
    Low,
    Medium,
    High,
    Certain
}

public enum CrashDiagnosisSeverity
{
    Info,
    Warning,
    Error
}

public enum CrashDiagnosisNature
{
    RootCause,
    ProbableCause,
    Symptom,
    Context
}