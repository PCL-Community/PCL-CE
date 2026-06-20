extern alias PclApplication;

using System.Text.Json.Nodes;
using PCL.Core.App;
using PCL.Core.Utils.OS;
using MinecraftArgumentOperatingSystem = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftArgumentOperatingSystem;
using MinecraftArgumentRuleContext = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftArgumentRuleContext;
using MinecraftJvmArgumentRequest = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftJvmArgumentRequest;
using MinecraftJvmArgumentService = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftJvmArgumentService;
using MinecraftJvmIpPreference = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftJvmIpPreference;
using MinecraftLegacyGameArgumentRequest = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftLegacyGameArgumentRequest;
using MinecraftLaunchPlanRequest = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftLaunchPlanRequest;
using MinecraftLaunchPlanService = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftLaunchPlanService;
using MinecraftModernGameArgumentRequest = PclApplication::PCL.Application.Minecraft.Launch.Arguments.MinecraftModernGameArgumentRequest;
using OptiFineTweakerAdjustment = PclApplication::PCL.Application.Minecraft.Launch.Arguments.OptiFineTweakerAdjustment;

namespace PCL;

internal static class LauncherLaunchApplicationAdapter
{
    public static string BuildJvmArguments(
        McInstance instance,
        bool useModernArguments,
        string? customJvmArguments,
        int memoryMegabytes,
        string? nativesDirectory,
        LauncherJvmIpPreference preferredIpStack,
        IReadOnlyList<string> prefixArguments,
        IReadOnlyList<string> suffixArguments)
    {
        string? mainClass = instance.JsonObject["mainClass"]?.ToString();
        if (string.IsNullOrWhiteSpace(mainClass))
            throw new InvalidOperationException("Minecraft version JSON does not contain mainClass.");

        return MinecraftJvmArgumentService.Build(new MinecraftJvmArgumentRequest
        {
            VersionJson = instance.JsonObject,
            InheritedVersionJsons = GetInheritedVersionJsons(instance),
            RuleContext = CreateRuleContext(),
            MainClass = mainClass,
            CustomJvmArguments = customJvmArguments,
            MemoryMegabytes = memoryMegabytes,
            NativesDirectory = nativesDirectory,
            PreferredIpStack = preferredIpStack switch
            {
                LauncherJvmIpPreference.PreferV4 => MinecraftJvmIpPreference.PreferV4,
                LauncherJvmIpPreference.PreferV6 => MinecraftJvmIpPreference.PreferV6,
                _ => MinecraftJvmIpPreference.SystemDefault
            },
            PrefixArguments = prefixArguments,
            SuffixArguments = suffixArguments,
            UseModernArguments = useModernArguments
        }).Arguments;
    }

    public static LauncherLaunchPlanResult BuildLaunchPlan(
        McInstance instance,
        string jvmArguments,
        IReadOnlyDictionary<string, string> replacements,
        int javaMajorVersion,
        bool fullscreen,
        IReadOnlyList<string> extraArguments,
        string? customGameArguments,
        string? worldName,
        string? server,
        bool needsRetroWrapper)
    {
        string? legacyArguments = instance.JsonObject["minecraftArguments"]?.ToString();
        bool hasModernGameArguments = instance.JsonObject["arguments"]?["game"] is not null;
        var ruleContext = CreateRuleContext();
        var result = MinecraftLaunchPlanService.CreatePlan(new MinecraftLaunchPlanRequest
        {
            PrebuiltJvmArguments = jvmArguments,
            LegacyGame = string.IsNullOrEmpty(legacyArguments)
                ? null
                : new MinecraftLegacyGameArgumentRequest
                {
                    MinecraftArguments = legacyArguments,
                    NeedsRetroWrapper = needsRetroWrapper,
                    HasForge = instance.Info.HasForge,
                    HasLiteLoader = instance.Info.HasLiteLoader,
                    HasOptiFine = instance.Info.HasOptiFine
                },
            ModernGame = hasModernGameArguments
                ? new MinecraftModernGameArgumentRequest
                {
                    VersionJson = instance.JsonObject,
                    InheritedVersionJsons = GetInheritedVersionJsons(instance),
                    RuleContext = ruleContext,
                    HasForge = instance.Info.HasForge,
                    HasLiteLoader = instance.Info.HasLiteLoader,
                    HasOptiFine = instance.Info.HasOptiFine
                }
                : null,
            Replacements = replacements,
            JavaMajorVersion = javaMajorVersion,
            Fullscreen = fullscreen,
            ExtraArguments = extraArguments,
            CustomGameArguments = customGameArguments,
            WorldName = worldName,
            Server = server,
            ReleaseTime = ToUtcLikeOffset(instance.releaseTime),
            HasOptiFine = instance.Info.HasOptiFine
        });

        return new LauncherLaunchPlanResult(
            result.JvmArguments,
            result.GameArguments,
            result.Arguments,
            result.OptiFineTweakerAdjustment switch
            {
                OptiFineTweakerAdjustment.MovedForgeTweaker => LauncherOptiFineTweakerAdjustment.MovedForgeTweaker,
                OptiFineTweakerAdjustment.ReplacedPlainTweaker => LauncherOptiFineTweakerAdjustment.ReplacedPlainTweaker,
                _ => LauncherOptiFineTweakerAdjustment.None
            },
            result.ShouldWarnOptiFineAutoJoin);
    }

    private static IReadOnlyList<JsonObject> GetInheritedVersionJsons(McInstance instance)
    {
        List<JsonObject> inheritedVersions = [];
        var currentInstance = instance;
        while (!string.IsNullOrEmpty(currentInstance.InheritInstanceName))
        {
            currentInstance = new McInstance(currentInstance.InheritInstanceName);
            inheritedVersions.Add(currentInstance.JsonObject);
        }

        return inheritedVersions;
    }

    private static MinecraftArgumentRuleContext CreateRuleContext() => new()
    {
        OperatingSystem = OperatingSystem.IsWindows()
            ? MinecraftArgumentOperatingSystem.Win32
            : OperatingSystem.IsMacOS()
                ? MinecraftArgumentOperatingSystem.MacOs
                : OperatingSystem.IsLinux()
                    ? MinecraftArgumentOperatingSystem.Linux
                    : MinecraftArgumentOperatingSystem.Unknown,
        OperatingSystemVersion = Environment.OSVersion.Version.ToString(),
        Is32BitArchitecture = SystemInfo.Is32BitSystem,
        EnableQuickPlayFeatureArguments = false
    };

    private static DateTimeOffset? ToUtcLikeOffset(DateTime value)
    {
        if (value.Year <= 1)
            return null;

        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.Zero);
    }
}

internal sealed record LauncherGameArgumentsResult(
    string Arguments,
    LauncherOptiFineTweakerAdjustment OptiFineTweakerAdjustment);

internal enum LauncherOptiFineTweakerAdjustment
{
    None,
    MovedForgeTweaker,
    ReplacedPlainTweaker
}

internal enum LauncherJvmIpPreference
{
    SystemDefault,
    PreferV4,
    PreferV6
}

internal sealed record LauncherLaunchPlanResult(
    string JvmArguments,
    string GameArguments,
    string Arguments,
    LauncherOptiFineTweakerAdjustment OptiFineTweakerAdjustment,
    bool ShouldWarnOptiFineAutoJoin);
