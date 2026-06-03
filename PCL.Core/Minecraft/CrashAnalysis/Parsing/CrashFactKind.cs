namespace PCL.Core.Minecraft.CrashAnalysis;

public enum CrashFactKind
{
    JavaVersionDetected,
    JavaVendorDetected,
    JavaArchitectureDetected,
    JavaFatalErrorDetected,
    JavaOutOfMemoryDetected,
    JavaUnsupportedClassVersionDetected,
    JavaModuleAccessErrorDetected,
    NativeAccessViolationDetected,
    NativeLibraryInCrashFrame,
    OpenGlInitializationFailed,
    LwjglInitializationFailed,
    GpuVendorDetected,
    GpuDriverIssueHint,
    MinecraftVersionDetected,
    MinecraftCrashReportPresent,
    MinecraftMainException,
    MinecraftReportedException,
    MinecraftExitCodeDetected,
    LoaderDetected,
    LoaderVersionDetected,
    LoaderDependencyError,
    LoaderResolutionError,
    LoaderMixinError,
    LoaderTransformError,
    ModListDetected,
    ModCandidateDetected,
    DuplicateModDetected,
    MissingModDependencyDetected,
    ModVersionConflictDetected,
    ModFileCorrupted,
    ModFileNameInvalid,
    ResourcePackIssueDetected,
    ShaderIssueDetected,
    WorldBlockEntityIssueDetected,
    WorldEntityIssueDetected,
    ConfigParseIssueDetected,
    MemoryAllocationDetected,
    OsVersionDetected,
    ProcessBitnessDetected,
    LaunchArgumentDetected
}

public enum CrashFactConfidence
{
    Low,
    Medium,
    High
}