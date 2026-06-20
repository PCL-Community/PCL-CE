extern alias PclApplication;

using System.Globalization;
using System.Text.Json.Nodes;
using PCL.Core.Minecraft;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Domain.Minecraft.Java;
using PCL.Domain.Minecraft.Launch;
using PCL.Platform.Paths;
using CoreJavaBrand = PCL.Core.Minecraft.Java.JavaBrandType;
using CoreJavaEntry = PCL.Core.Minecraft.JavaEntry;
using CoreJavaPreference = PCL.Core.Minecraft.Java.UserPreference.JavaPreference;
using CoreAutoSelect = PCL.Core.Minecraft.Java.UserPreference.AutoSelect;
using CoreExistingJava = PCL.Core.Minecraft.Java.UserPreference.ExistingJava;
using CoreUseGlobalPreference = PCL.Core.Minecraft.Java.UserPreference.UseGlobalPreference;
using CoreUseRelativePath = PCL.Core.Minecraft.Java.UserPreference.UseRelativePath;
using JavaPreferenceParser = PclApplication::PCL.Application.Minecraft.Java.JavaPreferenceParser;
using JavaRuntimeArchitecture = PclApplication::PCL.Application.Minecraft.Java.JavaRuntimeArchitecture;
using JavaRuntimeDownloadPlan = PclApplication::PCL.Application.Minecraft.Java.JavaRuntimeDownloadPlan;
using JavaRuntimeDownloadPlanService = PclApplication::PCL.Application.Minecraft.Java.JavaRuntimeDownloadPlanService;
using JavaRuntimeOperatingSystem = PclApplication::PCL.Application.Minecraft.Java.JavaRuntimeOperatingSystem;
using JavaRuntimePlatform = PclApplication::PCL.Application.Minecraft.Java.JavaRuntimePlatform;
using JavaAcquisitionBlockReason = PclApplication::PCL.Application.Minecraft.Launch.JavaAcquisitionBlockReason;
using JavaRequirementResolution = PclApplication::PCL.Application.Minecraft.Launch.JavaRequirementResolution;
using JavaRuntimeRequirementResolver = PclApplication::PCL.Application.Minecraft.Launch.JavaRuntimeRequirementResolver;
using JavaRuntimeAcquisitionPlanner = PclApplication::PCL.Application.Minecraft.Launch.JavaRuntimeAcquisitionPlanner;
using JavaSelectionService = PclApplication::PCL.Application.Minecraft.Launch.JavaSelectionService;
using JavaVersionRange = PclApplication::PCL.Application.Minecraft.Launch.JavaVersionRange;

namespace PCL;

internal static class LauncherJavaApplicationAdapter
{
    public static JavaRequirementResolution ResolveJavaRequirement(McInstance instance) =>
        JavaRuntimeRequirementResolver.Resolve(CreateLaunchProfile(instance));

    public static CoreJavaPreference ParseInstanceJavaPreference(string? rawPreference, string launcherDirectory)
    {
        JavaPreference preference = JavaPreferenceParser.Parse(rawPreference, launcherDirectory);
        return preference switch
        {
            AutoSelectJavaPreference => new CoreAutoSelect(),
            ExistingJavaPreference existing => new CoreExistingJava(existing.JavaExecutablePath),
            UseRelativeJavaPreference relative => new CoreUseRelativePath(relative.RelativePath),
            UseGlobalJavaPreference => new CoreUseGlobalPreference(),
            _ => new CoreAutoSelect()
        };
    }

    public static LauncherJavaAcquisitionDecision PlanJavaAcquisition(
        JavaRequirementResolution requirement,
        McInstance instance)
    {
        var decision = JavaRuntimeAcquisitionPlanner.Plan(requirement, instance.Info.HasForge);
        return new LauncherJavaAcquisitionDecision(
            decision.CanAutoDownload,
            decision.JavaVersionCode,
            decision.DownloadComponent,
            decision.BlockReason switch
            {
                JavaAcquisitionBlockReason.LegacyForgeNeedsFixerOrJava7 =>
                    LauncherJavaAcquisitionBlockReason.LegacyForgeNeedsFixerOrJava7,
                JavaAcquisitionBlockReason.LegacyJava7Required =>
                    LauncherJavaAcquisitionBlockReason.LegacyJava7Required,
                JavaAcquisitionBlockReason.Java8Update141To320Required =>
                    LauncherJavaAcquisitionBlockReason.Java8Update141To320Required,
                JavaAcquisitionBlockReason.Java8Update141OrLaterRequired =>
                    LauncherJavaAcquisitionBlockReason.Java8Update141OrLaterRequired,
                _ => LauncherJavaAcquisitionBlockReason.None
            });
    }

    public static JavaRuntimeDownloadPlan CreateJavaRuntimeDownloadPlan(
        string requestedComponent)
    {
        JavaRuntimeDownloadPlanService service = new(new LauncherJavaRuntimeMetadataProvider());
        return service.CreatePlanAsync(
                requestedComponent,
                CreateCurrentJavaRuntimePlatform(),
                new DefaultPlatformPathProvider())
            .AsTask()
            .GetAwaiter()
            .GetResult();
    }

    public static CoreJavaEntry? SelectBestJava(
        IEnumerable<CoreJavaEntry> entries,
        Version minVersion,
        Version maxVersion)
    {
        var range = new JavaVersionRange(minVersion, maxVersion);
        var pairs = entries
            .Select(static entry => new JavaCandidatePair(entry, ToRuntimeCandidate(entry)))
            .ToArray();
        var selected = JavaSelectionService.SelectBestCandidate(
            pairs.Select(static pair => pair.Candidate),
            range);

        if (selected is null)
            return null;

        return pairs.FirstOrDefault(pair =>
            string.Equals(
                pair.Candidate.Installation.JavaExecutablePath,
                selected.Installation.JavaExecutablePath,
                StringComparison.OrdinalIgnoreCase)).Entry;
    }

    private static MinecraftLaunchProfile CreateLaunchProfile(McInstance instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var info = instance.Info;
        int? manifestJavaMajor = ReadInt(instance.JsonObject?["javaVersion"]?["majorVersion"]) ??
                                 ReadInt(instance.JsonVersion?["java_version"]);

        return new MinecraftLaunchProfile
        {
            InstanceId = instance.PathInstance,
            VanillaVersion = info.Valid ? info.vanilla : null,
            HasReliableVanillaVersion = info.Valid,
            ReleaseTime = ToUtcLikeOffset(instance.releaseTime),
            ManifestJavaMajorVersion = manifestJavaMajor,
            ManifestJavaComponent = ReadString(instance.JsonObject?["javaVersion"]?["component"]) ??
                                    ReadString(instance.JsonVersion?["java_component"]),
            HasOptiFine = info.HasOptiFine,
            HasForge = info.HasForge,
            ForgeVersion = info.Forge,
            HasCleanroom = info.HasCleanroom,
            CleanroomVersion = info.Cleanroom,
            HasFabric = info.HasFabric,
            HasLiteLoader = info.HasLiteLoader,
            HasLabyMod = info.HasLabyMod
        };
    }

    private static JavaRuntimeCandidate ToRuntimeCandidate(CoreJavaEntry entry)
    {
        var installation = entry.Installation;
        return new JavaRuntimeCandidate(
            new JavaInstallation(
                installation.JavaFolder,
                installation.JavaExePath,
                installation.JavawExePath,
                installation.Version,
                MapBrand(installation.Brand),
                MapArchitecture(installation.Architecture),
                installation.Is64Bit,
                installation.IsJre),
            entry.IsEnabled,
            installation.IsStillAvailable,
            MapSource(entry.Source));
    }

    private static JavaBrand MapBrand(CoreJavaBrand brand) =>
        Enum.TryParse(brand.ToString(), out JavaBrand result) ? result : JavaBrand.Unknown;

    private static JavaArchitecture MapArchitecture(MachineType architecture) =>
        architecture switch
        {
            MachineType.I386 => JavaArchitecture.X86,
            MachineType.AMD64 or MachineType.IA64 => JavaArchitecture.X64,
            MachineType.ARM or MachineType.ARMNT => JavaArchitecture.Arm,
            MachineType.ARM64 => JavaArchitecture.Arm64,
            _ => JavaArchitecture.Unknown
        };

    private static JavaSource MapSource(PCL.Core.Minecraft.Java.JavaSource source) =>
        Enum.TryParse(source.ToString(), out JavaSource result) ? result : JavaSource.AutoScanned;

    private static int? ReadInt(JsonNode? node)
    {
        if (node is null)
            return null;

        try
        {
            return node.GetValue<int>();
        }
        catch (InvalidOperationException)
        {
            return int.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonNode? node)
    {
        string? value = node?.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTimeOffset? ToUtcLikeOffset(DateTime value)
    {
        if (value.Year <= 1)
            return null;

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.Zero);
    }

    private sealed record JavaCandidatePair(CoreJavaEntry Entry, JavaRuntimeCandidate Candidate);

    private static JavaRuntimePlatform CreateCurrentJavaRuntimePlatform() =>
        new(
            JavaRuntimeOperatingSystem.Win32,
            SystemInfo.Is32BitSystem ? JavaRuntimeArchitecture.X86 : JavaRuntimeArchitecture.X64);
}

internal sealed record LauncherJavaAcquisitionDecision(
    bool CanAutoDownload,
    string? JavaVersionCode,
    string? DownloadComponent,
    LauncherJavaAcquisitionBlockReason BlockReason);

internal enum LauncherJavaAcquisitionBlockReason
{
    None,
    LegacyForgeNeedsFixerOrJava7,
    LegacyJava7Required,
    Java8Update141To320Required,
    Java8Update141OrLaterRequired
}
