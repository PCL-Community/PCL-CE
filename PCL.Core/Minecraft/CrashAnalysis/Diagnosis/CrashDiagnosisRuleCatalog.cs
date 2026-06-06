namespace PCL.Core.Minecraft.CrashAnalysis;

public static partial class CrashDiagnosisRuleCatalog
{
    public static IReadOnlyList<CrashDiagnosisRule> Create()
    {
        return
        [
            new NoUsefulLogRule(),
            new ManualDebugCrashRule(),
            new MemoryRule(),
            new JavaCompatibilityRule(),
            new JavaLaunchRule(),
            new JavaVendorRule(),
            new JavaArchitectureRule(),
            new LwjglNativeLoadRule(),
            new GraphicsOpenGlRule(),
            new GraphicsDriverNativeCrashRule(),
            new GameResourceOrShaderRule(),
            new LoaderDependencyRule(),
            new LoaderVersionRule(),
            new ForgeModLoadingRule(),
            new CrashReportSuspectedModRule(),
            new MixinTransformRule(),
            new ModSetConflictRule(),
            new DuplicateModRule(),
            new ModFileRule(),
            new ModConfigRule(),
            new WorldContentRule(),
            new DataPackRule(),
            new RegistryRule(),
            new GameFileIntegrityRule(),
            new LibraryOrNativeRule(),
            new FileSystemRule(),
            new NativeJvmRule()
        ];
    }
}