// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Text.Json.Nodes;
using PCL.Application.Minecraft.Assets;
using PCL.Application.Minecraft.Launch.Arguments;
using PCL.Application.Minecraft.Launch.Libraries;
using PCL.Application.Minecraft.Launch.Natives;

namespace PCL.Application.Launching;

public sealed record MinecraftProcessLaunchRequest
{
    public required string VersionId { get; init; }
    public required string VersionJsonPath { get; init; }
    public required string InstanceDirectory { get; init; }
    public required string MinecraftRootDirectory { get; init; }
    public required string PlayerName { get; init; }
    public required string PlayerUuid { get; init; }
    public string AccessToken { get; init; } = "0";
    public string JavaExecutablePath { get; init; } = "java";
    public int MemoryMegabytes { get; init; } = 2048;
    public int Width { get; init; } = 854;
    public int Height { get; init; } = 480;
    public bool Fullscreen { get; init; }
    public bool IsolatedGameDirectory { get; init; }
    public string? CustomJvmArguments { get; init; }
    public string? CustomGameArguments { get; init; }
    public IReadOnlyList<string> ClasspathHeadEntries { get; init; } = [];
    public string? AuthlibInjectorPath { get; init; }
    public string? AuthlibServer { get; init; }
    public string? AuthlibPrefetchedMetadata { get; init; }
    public MinecraftJvmIpPreference PreferredIpStack { get; init; }
    public string? Server { get; init; }
    public DateTimeOffset? ReleaseTime { get; init; }
    public bool HasOptiFine { get; init; }
    public string? WorldName { get; init; }
}

public sealed record MinecraftProcessLaunchPlan(
    ProcessStartInfo StartInfo,
    string NativesDirectory,
    IReadOnlyList<string> ClasspathEntries,
    MinecraftNativeExtractionResult NativeExtraction);

public static class MinecraftProcessLaunchService
{
    public static async Task<MinecraftProcessLaunchPlan> CreatePlanAsync(
        MinecraftProcessLaunchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.VersionJsonPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.InstanceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftRootDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PlayerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PlayerUuid);

        JsonObject versionJson = await ReadJsonObjectAsync(request.VersionJsonPath, cancellationToken).ConfigureAwait(false);
        string minecraftRoot = Path.GetFullPath(request.MinecraftRootDirectory);
        string instanceDirectory = Path.GetFullPath(request.InstanceDirectory);
        IReadOnlyList<InheritedVersionJson> inheritedVersions = await ReadInheritedVersionJsonsAsync(
                versionJson,
                minecraftRoot,
                cancellationToken)
            .ConfigureAwait(false);
        JsonObject[] inheritedVersionJsons = inheritedVersions.Select(static version => version.Json).ToArray();

        string mainClass = FindString(versionJson, inheritedVersionJsons, "mainClass")
                           ?? throw new FormatException("version.json 缺少 mainClass。");
        string gameDirectory = request.IsolatedGameDirectory ? instanceDirectory : minecraftRoot;
        string nativesDirectory = Path.Combine(instanceDirectory, "natives");
        string versionJar = Path.Combine(instanceDirectory, request.VersionId + ".jar");

        MinecraftArgumentRuleContext ruleContext = CreateRuleContext();
        IReadOnlyList<MinecraftLibraryToken> libraries = ResolveLibraries(versionJson, inheritedVersionJsons, minecraftRoot, instanceDirectory);

        MinecraftNativeExtractionResult nativeExtraction = MinecraftNativeExtractionService.Extract(
            new MinecraftNativeExtractionRequest
            {
                ArchivePaths = libraries
                    .Where(static library => library.IsNatives && File.Exists(library.LocalPath))
                    .Select(static library => library.LocalPath)
                    .ToArray(),
                TargetDirectory = nativesDirectory,
                OperatingSystem = GetNativeOperatingSystem()
            });

        MinecraftClasspathPlan classpath = MinecraftClasspathPlanner.CreatePlan(
            new MinecraftClasspathPlanRequest
            {
                Libraries = libraries,
                ClasspathHeadEntries = request.ClasspathHeadEntries,
                BundledClasspathEntries = CreateBundledClasspathEntries(versionJar, inheritedVersions, minecraftRoot)
            });
        string classpathText = string.Join(Path.PathSeparator, classpath.Entries);
        string assetIndexName = MinecraftAssetIndexResolver.GetIndexName(
            new MinecraftAssetIndexNameRequest
            {
                VersionJson = versionJson,
                InheritedVersionJsons = inheritedVersionJsons
            });

        MinecraftLaunchPlanResult launchPlan = MinecraftLaunchPlanService.CreatePlan(
            new MinecraftLaunchPlanRequest
            {
                Jvm = new MinecraftJvmArgumentRequest
                {
                    VersionJson = versionJson,
                    InheritedVersionJsons = inheritedVersionJsons,
                    RuleContext = ruleContext,
                    MainClass = mainClass,
                    CustomJvmArguments = request.CustomJvmArguments,
                    MemoryMegabytes = request.MemoryMegabytes,
                    NativesDirectory = nativesDirectory,
                    PreferredIpStack = request.PreferredIpStack,
                    PrefixArguments = CreateJvmPrefixArguments(request),
                    UseModernArguments = HasArguments(versionJson, inheritedVersionJsons, "jvm")
                },
                ModernGame = HasArguments(versionJson, inheritedVersionJsons, "game")
                    ? new MinecraftModernGameArgumentRequest
                    {
                        VersionJson = versionJson,
                        InheritedVersionJsons = inheritedVersionJsons,
                        RuleContext = ruleContext
                    }
                    : null,
                LegacyGame = FindString(versionJson, inheritedVersionJsons, "minecraftArguments") is { } minecraftArguments
                    ? new MinecraftLegacyGameArgumentRequest
                    {
                        MinecraftArguments = minecraftArguments
                    }
                    : null,
                Replacements = CreateReplacements(request, minecraftRoot, gameDirectory, nativesDirectory, classpathText, assetIndexName),
                JavaMajorVersion = 17,
                Fullscreen = request.Fullscreen,
                CustomGameArguments = request.CustomGameArguments,
                WorldName = request.WorldName,
                Server = request.Server,
                ReleaseTime = request.ReleaseTime,
                HasOptiFine = request.HasOptiFine
            });

        ProcessStartInfo startInfo = new()
        {
            FileName = request.JavaExecutablePath,
            Arguments = launchPlan.Arguments,
            WorkingDirectory = gameDirectory,
            UseShellExecute = false
        };
        return new MinecraftProcessLaunchPlan(startInfo, nativesDirectory, classpath.Entries, nativeExtraction);
    }

    private static Dictionary<string, string> CreateReplacements(
        MinecraftProcessLaunchRequest request,
        string minecraftRoot,
        string gameDirectory,
        string nativesDirectory,
        string classpath,
        string assetIndexName) =>
        new(StringComparer.Ordinal)
        {
            ["${natives_directory}"] = Quote(nativesDirectory),
            ["${launcher_name}"] = "PCL-N",
            ["${launcher_version}"] = "Avalonia",
            ["${classpath}"] = Quote(classpath),
            ["${auth_player_name}"] = request.PlayerName,
            ["${version_name}"] = request.VersionId,
            ["${game_directory}"] = Quote(gameDirectory),
            ["${assets_root}"] = Quote(Path.Combine(minecraftRoot, "assets")),
            ["${assets_index_name}"] = assetIndexName,
            ["${auth_uuid}"] = request.PlayerUuid.Replace("-", string.Empty, StringComparison.Ordinal),
            ["${auth_access_token}"] = request.AccessToken,
            ["${clientid}"] = Guid.NewGuid().ToString("N"),
            ["${auth_xuid}"] = "0",
            ["${user_type}"] = "msa",
            ["${version_type}"] = "PCL-N",
            ["${resolution_width}"] = request.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["${resolution_height}"] = request.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["${quickPlayPath}"] = string.Empty,
            ["${game_assets}"] = Quote(Path.Combine(minecraftRoot, "assets", "virtual", "legacy"))
        };

    private static async Task<IReadOnlyList<InheritedVersionJson>> ReadInheritedVersionJsonsAsync(
        JsonObject versionJson,
        string minecraftRoot,
        CancellationToken cancellationToken)
    {
        List<InheritedVersionJson> result = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        JsonObject current = versionJson;
        while (current["inheritsFrom"]?.ToString() is { Length: > 0 } inheritedId)
        {
            if (!seen.Add(inheritedId))
                throw new FormatException("version.json 存在循环继承：" + inheritedId);

            string inheritedJsonPath = Path.Combine(minecraftRoot, "versions", inheritedId, inheritedId + ".json");
            if (!File.Exists(inheritedJsonPath))
                throw new FileNotFoundException("缺少继承版本描述：" + inheritedId, inheritedJsonPath);

            JsonObject inheritedJson = await ReadJsonObjectAsync(inheritedJsonPath, cancellationToken).ConfigureAwait(false);
            result.Add(new InheritedVersionJson(inheritedId, inheritedJson));
            current = inheritedJson;
        }

        return result;
    }

    private static List<MinecraftLibraryToken> ResolveLibraries(
        JsonObject versionJson,
        IReadOnlyList<JsonObject> inheritedVersionJsons,
        string minecraftRoot,
        string instanceDirectory)
    {
        List<MinecraftLibraryToken> result = [];
        AddResolvedLibraries(result, versionJson, minecraftRoot, instanceDirectory);
        foreach (JsonObject inheritedVersionJson in inheritedVersionJsons)
            AddResolvedLibraries(result, inheritedVersionJson, minecraftRoot, instanceDirectory);

        List<MinecraftLibraryToken> deduplicated = [];
        HashSet<string> seen = new(GetPathComparer());
        foreach (MinecraftLibraryToken library in result)
        {
            if (seen.Add(library.LocalPath))
                deduplicated.Add(library);
        }

        return deduplicated;
    }

    private static void AddResolvedLibraries(
        List<MinecraftLibraryToken> target,
        JsonObject versionJson,
        string minecraftRoot,
        string instanceDirectory)
    {
        target.AddRange(MinecraftLibraryResolver.Resolve(
            new MinecraftLibraryResolutionRequest
            {
                VersionJson = versionJson,
                MinecraftRootDirectory = minecraftRoot,
                TargetInstanceDirectory = instanceDirectory,
                OperatingSystem = GetLibraryOperatingSystem(),
                Is64BitArchitecture = Environment.Is64BitOperatingSystem,
                OperatingSystemVersion = Environment.OSVersion.VersionString
            }));
    }

    private static List<string> CreateBundledClasspathEntries(
        string versionJar,
        IReadOnlyList<InheritedVersionJson> inheritedVersions,
        string minecraftRoot)
    {
        List<string> entries = [];
        if (File.Exists(versionJar))
            entries.Add(versionJar);

        foreach (InheritedVersionJson inheritedVersion in inheritedVersions)
        {
            string inheritedJar = Path.Combine(
                minecraftRoot,
                "versions",
                inheritedVersion.VersionId,
                inheritedVersion.VersionId + ".jar");
            if (File.Exists(inheritedJar))
                entries.Add(inheritedJar);
        }

        return entries;
    }

    private static string? FindString(
        JsonObject versionJson,
        IReadOnlyList<JsonObject> inheritedVersionJsons,
        string propertyName)
    {
        string? value = EmptyToNull(versionJson[propertyName]?.ToString());
        if (value is not null)
            return value;

        foreach (JsonObject inheritedVersionJson in inheritedVersionJsons)
        {
            value = EmptyToNull(inheritedVersionJson[propertyName]?.ToString());
            if (value is not null)
                return value;
        }

        return null;
    }

    private static bool HasArguments(
        JsonObject versionJson,
        IReadOnlyList<JsonObject> inheritedVersionJsons,
        string argumentName)
    {
        if (versionJson["arguments"]?[argumentName] is not null)
            return true;

        return inheritedVersionJsons.Any(inheritedVersionJson => inheritedVersionJson["arguments"]?[argumentName] is not null);
    }

    private static async Task<JsonObject> ReadJsonObjectAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 64 * 1024,
            useAsync: true);
        JsonNode? node = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return node as JsonObject
               ?? throw new FormatException("JSON 根节点不是对象：" + path);
    }

    private static MinecraftArgumentRuleContext CreateRuleContext() => new()
    {
        OperatingSystem = GetArgumentOperatingSystem(),
        OperatingSystemVersion = Environment.OSVersion.VersionString,
        Is32BitArchitecture = !Environment.Is64BitOperatingSystem,
        EnableQuickPlayFeatureArguments = false
    };

    private static MinecraftArgumentOperatingSystem GetArgumentOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return MinecraftArgumentOperatingSystem.Win32;
        if (OperatingSystem.IsLinux())
            return MinecraftArgumentOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return MinecraftArgumentOperatingSystem.MacOs;
        return MinecraftArgumentOperatingSystem.Unknown;
    }

    private static MinecraftLibraryOperatingSystem GetLibraryOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return MinecraftLibraryOperatingSystem.Win32;
        if (OperatingSystem.IsLinux())
            return MinecraftLibraryOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return MinecraftLibraryOperatingSystem.MacOs;
        return MinecraftLibraryOperatingSystem.Unknown;
    }

    private static MinecraftNativeOperatingSystem GetNativeOperatingSystem()
    {
        if (OperatingSystem.IsWindows())
            return MinecraftNativeOperatingSystem.Win32;
        if (OperatingSystem.IsLinux())
            return MinecraftNativeOperatingSystem.Linux;
        if (OperatingSystem.IsMacOS())
            return MinecraftNativeOperatingSystem.MacOs;
        return MinecraftNativeOperatingSystem.Unknown;
    }

    private static string Quote(string value) =>
        value.Contains(' ', StringComparison.Ordinal) ? "\"" + value + "\"" : value;

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static StringComparer GetPathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static IReadOnlyList<string> CreateJvmPrefixArguments(MinecraftProcessLaunchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AuthlibInjectorPath) ||
            string.IsNullOrWhiteSpace(request.AuthlibServer))
        {
            return [];
        }

        string javaAgent = "-javaagent:" + Quote(request.AuthlibInjectorPath) + "=" + request.AuthlibServer;
        if (!string.IsNullOrWhiteSpace(request.AuthlibPrefetchedMetadata))
        {
            javaAgent += " -Dauthlibinjector.side=client -Dauthlibinjector.yggdrasil.prefetched=" +
                         Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.AuthlibPrefetchedMetadata));
        }
        else
        {
            javaAgent += " -Dauthlibinjector.side=client";
        }

        return [javaAgent];
    }

    private sealed record InheritedVersionJson(string VersionId, JsonObject Json);
}
