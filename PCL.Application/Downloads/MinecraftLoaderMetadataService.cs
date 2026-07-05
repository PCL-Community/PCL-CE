// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.IO.Net;

namespace PCL.Application.Downloads;

public enum MinecraftLoaderKind
{
    Fabric,
    Quilt
}

public sealed record MinecraftLoaderInstallRequest(MinecraftLoaderKind Kind, string LoaderVersion);

public sealed record MinecraftLoaderVersionEntry(MinecraftLoaderKind Kind, string Version, bool Stable)
{
    public string DisplayVersion => Version.Replace("+build", string.Empty, StringComparison.Ordinal);
}

public sealed record MinecraftLoaderLibrary(string Name, string? Url);

public sealed record MinecraftLoaderInstallMetadata(
    MinecraftLoaderKind Kind,
    string LoaderVersion,
    string LoaderMaven,
    string MappingMaven,
    string MappingMavenRepository,
    string MainClass,
    IReadOnlyList<MinecraftLoaderLibrary> Libraries,
    int? MinimumJavaVersion);

public interface IMinecraftLoaderMetadataService
{
    Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLoaderVersionsAsync(
        MinecraftLoaderKind kind,
        string gameVersion,
        CancellationToken cancellationToken = default);

    Task<MinecraftLoaderInstallMetadata> GetLoaderInstallMetadataAsync(
        MinecraftLoaderInstallRequest request,
        string gameVersion,
        CancellationToken cancellationToken = default);
}

public sealed class MinecraftLoaderMetadataService : IMinecraftLoaderMetadataService
{
    private const string FabricMetadataRoot = "https://meta.fabricmc.net/v2/versions/loader/";
    private const string FabricMavenRoot = "https://maven.fabricmc.net/";
    private const string QuiltMetadataRoot = "https://meta.quiltmc.org/v3/versions/loader/";
    private const string QuiltMavenRoot = "https://maven.quiltmc.org/repository/release/";

    private readonly HttpClient _httpClient;

    public MinecraftLoaderMetadataService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? PortableHttp.Client;
    }

    public async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLoaderVersionsAsync(
        MinecraftLoaderKind kind,
        string gameVersion,
        CancellationToken cancellationToken = default)
    {
        JsonArray versions = await GetLoaderMetadataArrayAsync(kind, gameVersion, cancellationToken).ConfigureAwait(false);
        List<MinecraftLoaderVersionEntry> result = new(versions.Count);
        foreach (JsonNode? node in versions)
        {
            if (node is not JsonObject entry)
                continue;

            string? version = entry["loader"]?["version"]?.ToString();
            if (string.IsNullOrWhiteSpace(version))
                continue;

            result.Add(new MinecraftLoaderVersionEntry(kind, version, IsStable(kind, entry, version)));
        }

        return result;
    }

    public async Task<MinecraftLoaderInstallMetadata> GetLoaderInstallMetadataAsync(
        MinecraftLoaderInstallRequest request,
        string gameVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LoaderVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        JsonArray versions = await GetLoaderMetadataArrayAsync(request.Kind, gameVersion, cancellationToken).ConfigureAwait(false);
        foreach (JsonNode? node in versions)
        {
            if (node is not JsonObject entry)
                continue;

            string? version = entry["loader"]?["version"]?.ToString();
            if (string.Equals(version, request.LoaderVersion, StringComparison.Ordinal))
                return CreateInstallMetadata(request.Kind, entry);
        }

        throw new InvalidOperationException($"未找到 {request.Kind} {request.LoaderVersion} 对 Minecraft {gameVersion} 的安装元数据。");
    }

    private async Task<JsonArray> GetLoaderMetadataArrayAsync(
        MinecraftLoaderKind kind,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        string url = GetMetadataEndpoint(kind) + Uri.EscapeDataString(NormalizeGameVersion(gameVersion));
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        ConfigureRequest(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        string json = await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
        JsonNode? node = JsonNode.Parse(json);
        return node as JsonArray
               ?? throw new FormatException($"加载器元数据不是数组：{url}");
    }

    private static MinecraftLoaderInstallMetadata CreateInstallMetadata(MinecraftLoaderKind kind, JsonObject entry)
    {
        JsonObject loader = entry["loader"] as JsonObject
                            ?? throw new FormatException("加载器元数据缺少 loader 节点。");
        JsonObject launcherMeta = entry["launcherMeta"] as JsonObject
                                  ?? throw new FormatException("加载器元数据缺少 launcherMeta 节点。");

        string loaderVersion = RequiredString(loader, "version");
        string loaderMaven = RequiredString(loader, "maven");
        (string mappingMaven, string mappingRepository) = GetMappingLibrary(kind, entry);
        string mainClass = ReadClientMainClass(launcherMeta);
        List<MinecraftLoaderLibrary> libraries = [];
        AddLauncherMetaLibraries(libraries, launcherMeta["libraries"]?["common"]);
        AddLauncherMetaLibraries(libraries, launcherMeta["libraries"]?["client"]);

        libraries.Add(new MinecraftLoaderLibrary(mappingMaven, mappingRepository));
        libraries.Add(new MinecraftLoaderLibrary(loaderMaven, kind == MinecraftLoaderKind.Fabric ? FabricMavenRoot : QuiltMavenRoot));

        return new MinecraftLoaderInstallMetadata(
            kind,
            loaderVersion,
            loaderMaven,
            mappingMaven,
            mappingRepository,
            mainClass,
            DeduplicateLibraries(libraries),
            TryReadInt32(launcherMeta["min_java_version"]));
    }

    private static (string Name, string Repository) GetMappingLibrary(MinecraftLoaderKind kind, JsonObject entry)
    {
        if (kind == MinecraftLoaderKind.Quilt && entry["hashed"] is JsonObject hashed)
            return (RequiredString(hashed, "maven"), QuiltMavenRoot);

        JsonObject intermediary = entry["intermediary"] as JsonObject
                                  ?? throw new FormatException("加载器元数据缺少 intermediary 节点。");
        return (RequiredString(intermediary, "maven"), FabricMavenRoot);
    }

    private static void AddLauncherMetaLibraries(List<MinecraftLoaderLibrary> libraries, JsonNode? node)
    {
        if (node is not JsonArray array)
            return;

        foreach (JsonNode? item in array)
        {
            if (item is not JsonObject library)
                continue;

            string? name = library["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            libraries.Add(new MinecraftLoaderLibrary(name, EmptyToNull(library["url"]?.ToString())));
        }
    }

    private static List<MinecraftLoaderLibrary> DeduplicateLibraries(List<MinecraftLoaderLibrary> libraries)
    {
        List<MinecraftLoaderLibrary> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (MinecraftLoaderLibrary library in libraries)
        {
            if (seen.Add(library.Name))
                result.Add(library);
        }

        return result;
    }

    private static string ReadClientMainClass(JsonObject launcherMeta)
    {
        JsonNode? mainClass = launcherMeta["mainClass"];
        if (mainClass is JsonObject mainClassObject)
            return RequiredString(mainClassObject, "client");

        string? value = mainClass?.ToString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new FormatException("加载器元数据缺少客户端 mainClass。")
            : value;
    }

    private static string RequiredString(JsonObject source, string propertyName)
    {
        string? value = source[propertyName]?.ToString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new FormatException("加载器元数据缺少字段：" + propertyName)
            : value;
    }

    private static bool IsStable(MinecraftLoaderKind kind, JsonObject entry, string version)
    {
        if (entry["loader"]?["stable"] is JsonNode stableNode &&
            bool.TryParse(stableNode.ToString(), out bool stable))
        {
            return stable;
        }

        return kind == MinecraftLoaderKind.Quilt &&
               !version.Contains("alpha", StringComparison.OrdinalIgnoreCase) &&
               !version.Contains("beta", StringComparison.OrdinalIgnoreCase) &&
               !version.Contains("rc", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetMetadataEndpoint(MinecraftLoaderKind kind) =>
        kind switch
        {
            MinecraftLoaderKind.Fabric => FabricMetadataRoot,
            MinecraftLoaderKind.Quilt => QuiltMetadataRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string NormalizeGameVersion(string version) =>
        version.Replace("∞", "infinite", StringComparison.Ordinal)
            .Replace("Combat Test 7c", "1.16_combat-3", StringComparison.Ordinal);

    private static int? TryReadInt32(JsonNode? node)
    {
        if (node is null)
            return null;

        return int.TryParse(node.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static void ConfigureRequest(HttpRequestMessage request)
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        string language = CultureInfo.CurrentUICulture.Name;
        request.Headers.AcceptLanguage.ParseAdd(string.IsNullOrWhiteSpace(language) ? "zh-CN" : language);
    }
}
