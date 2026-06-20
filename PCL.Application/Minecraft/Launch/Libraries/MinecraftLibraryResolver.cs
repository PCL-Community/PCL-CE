// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Application.Minecraft.Launch.Arguments;

namespace PCL.Application.Minecraft.Launch.Libraries;

public static class MinecraftLibraryResolver
{
    public static IReadOnlyList<MinecraftLibraryToken> Resolve(MinecraftLibraryResolutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MinecraftRootDirectory);

        JsonArray? libraries = request.VersionJson["libraries"]?.AsArray();
        if (libraries is null)
            return [];

        List<MinecraftLibraryToken> result = [];
        MinecraftArgumentRuleContext ruleContext = CreateRuleContext(request);
        string minecraftRoot = Path.GetFullPath(request.MinecraftRootDirectory);

        foreach (JsonNode? libraryNode in libraries)
        {
            if (libraryNode is null || libraryNode.GetValueKind() != JsonValueKind.Object)
                continue;

            JsonObject library = libraryNode.AsObject();
            if (!MinecraftLaunchArgumentService.IsRuleAllowed(library["rules"], ruleContext))
                continue;

            string? originalName = library["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(originalName))
                continue;

            bool isLocal = string.Equals(library["hint"]?.ToString(), "local", StringComparison.Ordinal);
            string? rootUrl = BuildRootUrl(library["url"]?.ToString(), originalName);

            if (library["natives"] is null)
            {
                result.Add(ResolveArtifact(library, originalName, rootUrl, minecraftRoot, request.TargetInstanceDirectory, isLocal));
                continue;
            }

            MinecraftLibraryToken? nativeToken = ResolveNative(library, originalName, rootUrl, minecraftRoot, request, isLocal);
            if (nativeToken is not null)
                result.Add(nativeToken);
        }

        return result;
    }

    public static string GetCoordinatePath(
        string coordinate,
        string minecraftRootDirectory,
        bool includeMinecraftRoot = true)
    {
        string[] parts = coordinate.Split(':');
        if (parts.Length < 3)
            throw new FormatException($"Invalid library coordinate: {coordinate}");

        string relativePath = Path.Combine(
            parts[0].Replace('.', Path.DirectorySeparatorChar),
            parts[1],
            parts[2],
            parts[1] + "-" + parts[2] + ".jar");

        return includeMinecraftRoot
            ? Path.Combine(minecraftRootDirectory, "libraries", relativePath)
            : relativePath;
    }

    private static MinecraftLibraryToken ResolveArtifact(
        JsonObject library,
        string originalName,
        string? rootUrl,
        string minecraftRoot,
        string? targetInstanceDirectory,
        bool isLocal)
    {
        JsonNode? artifact = library["downloads"]?["artifact"];
        string localPath = isLocal && !string.IsNullOrWhiteSpace(targetInstanceDirectory)
            ? Path.Combine(targetInstanceDirectory, "libraries", GetLocalLibraryFileName(originalName))
            : GetCoordinatePath(originalName, minecraftRoot);

        if (artifact is not null)
        {
            localPath = artifact["path"] is null
                ? GetCoordinatePath(originalName, minecraftRoot)
                : Path.Combine(minecraftRoot, "libraries", NormalizeManifestPath(artifact["path"]!.ToString()));

            return CreateToken(
                originalName,
                localPath,
                rootUrl ?? EmptyToNull(artifact["url"]?.ToString()),
                EmptyToNull(artifact["sha1"]?.ToString()),
                ParseSize(artifact["size"]),
                isNatives: false,
                isLocal);
        }

        return CreateToken(originalName, localPath, rootUrl, sha1: null, size: 0, isNatives: false, isLocal);
    }

    private static MinecraftLibraryToken? ResolveNative(
        JsonObject library,
        string originalName,
        string? rootUrl,
        string minecraftRoot,
        MinecraftLibraryResolutionRequest request,
        bool isLocal)
    {
        string? nativeClassifier = GetNativeClassifier(library["natives"], request.OperatingSystem, request.Is64BitArchitecture);
        if (string.IsNullOrWhiteSpace(nativeClassifier))
            return null;

        JsonNode? classifier = library["downloads"]?["classifiers"]?[nativeClassifier];
        if (classifier is null)
        {
            string fallbackKey = GetFallbackNativeClassifierKey(request.OperatingSystem);
            if (!string.Equals(fallbackKey, nativeClassifier, StringComparison.Ordinal))
                classifier = library["downloads"]?["classifiers"]?[fallbackKey];
        }

        if (classifier is not null)
        {
            string localPath = classifier["path"] is null
                ? GetNativeCoordinatePath(originalName, minecraftRoot, nativeClassifier)
                : Path.Combine(minecraftRoot, "libraries", NormalizeManifestPath(classifier["path"]!.ToString()));

            return CreateToken(
                originalName,
                localPath,
                rootUrl ?? EmptyToNull(classifier["url"]?.ToString()),
                EmptyToNull(classifier["sha1"]?.ToString()),
                ParseSize(classifier["size"]),
                isNatives: true,
                isLocal);
        }

        return CreateToken(
            originalName,
            GetNativeCoordinatePath(originalName, minecraftRoot, nativeClassifier),
            rootUrl,
            sha1: null,
            size: 0,
            isNatives: true,
            isLocal);
    }

    private static MinecraftLibraryToken CreateToken(
        string originalName,
        string localPath,
        string? url,
        string? sha1,
        long size,
        bool isNatives,
        bool isLocal) =>
        new()
        {
            OriginalName = originalName,
            NameWithoutVersion = GetNameWithoutVersion(originalName),
            Url = EmptyToNull(url),
            LocalPath = localPath,
            Sha1 = EmptyToNull(sha1),
            Size = size,
            IsNatives = isNatives,
            IsLocal = isLocal
        };

    private static MinecraftArgumentRuleContext CreateRuleContext(MinecraftLibraryResolutionRequest request) => new()
    {
        OperatingSystem = request.OperatingSystem switch
        {
            MinecraftLibraryOperatingSystem.Win32 => MinecraftArgumentOperatingSystem.Win32,
            MinecraftLibraryOperatingSystem.Linux => MinecraftArgumentOperatingSystem.Linux,
            MinecraftLibraryOperatingSystem.MacOs => MinecraftArgumentOperatingSystem.MacOs,
            _ => MinecraftArgumentOperatingSystem.Unknown
        },
        OperatingSystemVersion = request.OperatingSystemVersion,
        Is32BitArchitecture = !request.Is64BitArchitecture,
        EnableQuickPlayFeatureArguments = false
    };

    private static string? BuildRootUrl(string? rootUrl, string coordinate)
    {
        rootUrl = EmptyToNull(rootUrl);
        return rootUrl is null
            ? null
            : rootUrl + GetCoordinatePath(coordinate, minecraftRootDirectory: string.Empty, includeMinecraftRoot: false)
                .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string? GetNativeClassifier(JsonNode? nativesNode, MinecraftLibraryOperatingSystem operatingSystem, bool is64Bit)
    {
        if (nativesNode is null)
            return null;

        string nativeKey = GetNativeOsKey(operatingSystem);
        return EmptyToNull(nativesNode[nativeKey]?.ToString())
            ?.Replace("${arch}", is64Bit ? "64" : "32", StringComparison.Ordinal);
    }

    private static string GetNativeOsKey(MinecraftLibraryOperatingSystem operatingSystem) =>
        operatingSystem switch
        {
            MinecraftLibraryOperatingSystem.Win32 => "windows",
            MinecraftLibraryOperatingSystem.Linux => "linux",
            MinecraftLibraryOperatingSystem.MacOs => "osx",
            _ => "unknown"
        };

    private static string GetFallbackNativeClassifierKey(MinecraftLibraryOperatingSystem operatingSystem) =>
        "natives-" + GetNativeOsKey(operatingSystem);

    private static string GetNativeCoordinatePath(string coordinate, string minecraftRoot, string classifier)
    {
        string artifactPath = GetCoordinatePath(coordinate, minecraftRoot);
        return Path.ChangeExtension(artifactPath, null) + "-" + classifier + ".jar";
    }

    private static string NormalizeManifestPath(string manifestPath) =>
        manifestPath.Replace('/', Path.DirectorySeparatorChar);

    private static long ParseSize(JsonNode? node)
    {
        if (node is null)
            return 0;

        string value = node.ToString();
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result)
            ? result
            : 0;
    }

    private static string GetLocalLibraryFileName(string coordinate)
    {
        int firstColonIndex = coordinate.IndexOf(':', StringComparison.Ordinal);
        string tail = firstColonIndex >= 0 ? coordinate[(firstColonIndex + 1)..] : coordinate;
        return tail.Replace(':', '-') + ".jar";
    }

    private static string? GetNameWithoutVersion(string originalName)
    {
        string[] parts = originalName.Split(':');
        if (parts.Length < 3)
            return null;

        List<string> nameParts = [.. parts];
        nameParts.RemoveAt(2);
        return string.Join(':', nameParts);
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
