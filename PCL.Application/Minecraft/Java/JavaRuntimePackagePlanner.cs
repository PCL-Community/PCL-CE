// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;

namespace PCL.Application.Minecraft.Java;

public static class JavaRuntimePackagePlanner
{
    private static readonly HashSet<string> IgnoredSha1 =
    [
        "12976a6c2b227cbac58969c1455444596c894656",
        "c80e4bab46e34d02826eab226a4441d0970f2aba",
        "84d2102ad171863db04e7ee22a259d1f6c5de4a5"
    ];

    public static JavaRuntimePackageDescriptor SelectPackage(
        string runtimeIndexJson,
        JavaRuntimePlatform platform,
        string requestedComponent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIndexJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedComponent);

        using JsonDocument document = JsonDocument.Parse(runtimeIndexJson);
        string platformKey = platform.ToMojangKey();
        if (!document.RootElement.TryGetProperty(platformKey, out JsonElement platformElement))
            throw new InvalidOperationException($"Mojang 未提供当前平台的 Java 运行时：{platformKey}");

        if (platformElement.TryGetProperty(requestedComponent, out JsonElement exactComponent))
            return ReadComponent(requestedComponent, exactComponent, requestedComponent);

        foreach (JsonProperty componentProperty in platformElement.EnumerateObject())
        {
            JsonElement firstVersion = GetFirstVersion(componentProperty.Value, componentProperty.Name);
            string versionName = ReadRequiredString(firstVersion, "version", "name");
            if (versionName.StartsWith(requestedComponent, StringComparison.OrdinalIgnoreCase))
                return CreateDescriptor(componentProperty.Name, firstVersion);
        }

        throw new InvalidOperationException($"未能找到所需的 Java {requestedComponent}");
    }

    public static JavaRuntimeDownloadPlan CreateDownloadPlan(
        JavaRuntimePackageDescriptor package,
        string manifestJson,
        string runtimeRootDirectory)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRootDirectory);

        string targetDirectory = Path.GetFullPath(Path.Combine(runtimeRootDirectory, package.ComponentName));
        using JsonDocument document = JsonDocument.Parse(manifestJson);
        if (!document.RootElement.TryGetProperty("files", out JsonElement filesElement))
            throw new InvalidOperationException("Java runtime manifest 缺少 files 节点");

        List<JavaRuntimeDownloadFile> files = new(filesElement.GetPropertyCount());
        foreach (JsonProperty fileProperty in filesElement.EnumerateObject())
        {
            if (!TryReadRawDownload(fileProperty.Value, out string? url, out string? sha1, out long size))
                continue;
            if (IgnoredSha1.Contains(sha1))
                continue;

            string targetPath = ResolveTargetPath(targetDirectory, fileProperty.Name);
            files.Add(new JavaRuntimeDownloadFile(fileProperty.Name, targetPath, url, sha1, size));
        }

        return new JavaRuntimeDownloadPlan(
            package.ComponentName,
            package.VersionName,
            package.ManifestUrl,
            targetDirectory,
            files);
    }

    private static JavaRuntimePackageDescriptor ReadComponent(
        string componentName,
        JsonElement componentElement,
        string requestedComponent)
    {
        try
        {
            return CreateDescriptor(componentName, GetFirstVersion(componentElement, componentName));
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Mojang 未提供所需的 Java {requestedComponent}", ex);
        }
    }

    private static JavaRuntimePackageDescriptor CreateDescriptor(string componentName, JsonElement versionElement)
    {
        return new JavaRuntimePackageDescriptor(
            componentName,
            ReadRequiredString(versionElement, "version", "name"),
            ReadRequiredString(versionElement, "manifest", "url"));
    }

    private static JsonElement GetFirstVersion(JsonElement componentElement, string componentName)
    {
        if (componentElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"Java runtime component 格式无效：{componentName}");

        foreach (JsonElement versionElement in componentElement.EnumerateArray())
            return versionElement;

        throw new InvalidOperationException($"Java runtime component 没有可用版本：{componentName}");
    }

    private static bool TryReadRawDownload(
        JsonElement fileElement,
        out string url,
        out string sha1,
        out long size)
    {
        url = string.Empty;
        sha1 = string.Empty;
        size = 0;

        if (!fileElement.TryGetProperty("downloads", out JsonElement downloadsElement) ||
            !downloadsElement.TryGetProperty("raw", out JsonElement rawElement))
            return false;

        url = ReadRequiredString(rawElement, "url");
        sha1 = ReadRequiredString(rawElement, "sha1");
        size = rawElement.GetProperty("size").GetInt64();
        return true;
    }

    private static string ResolveTargetPath(string targetDirectory, string relativePath)
    {
        string fullTargetDirectory = EnsureTrailingSeparator(Path.GetFullPath(targetDirectory));
        string targetPath = Path.GetFullPath(Path.Combine(fullTargetDirectory, relativePath));
        if (!targetPath.StartsWith(fullTargetDirectory, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
            throw new InvalidOperationException($"{targetPath} 不在 {targetDirectory} 中");

        return targetPath;
    }

    private static string EnsureTrailingSeparator(string directory)
    {
        return directory.EndsWith(Path.DirectorySeparatorChar) || directory.EndsWith(Path.AltDirectorySeparatorChar)
            ? directory
            : string.Concat(directory, Path.DirectorySeparatorChar);
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        string? value = element.GetProperty(propertyName).GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Java runtime manifest 缺少字段：{propertyName}");

        return value;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName, string nestedPropertyName)
    {
        string? value = element.GetProperty(propertyName).GetProperty(nestedPropertyName).GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Java runtime manifest 缺少字段：{propertyName}.{nestedPropertyName}");

        return value;
    }
}
