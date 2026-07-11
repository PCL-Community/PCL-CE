// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using PCL.Core.IO.Net;

namespace PCL.Application.Downloads;

public enum MinecraftInstallAddonKind
{
    FabricApi,
    LegacyFabricApi,
    Qsl,
    OptiFabric
}

public sealed record MinecraftInstallAddonVersionEntry(
    MinecraftInstallAddonKind Kind,
    string Version,
    string FileName,
    string Url,
    string? Sha1,
    long Size,
    bool Stable);

public sealed record MinecraftInstallAddonRequest(
    MinecraftInstallAddonKind Kind,
    string Version,
    string FileName,
    string Url,
    string? Sha1,
    long Size);

public interface IMinecraftInstallAddonMetadataService
{
    Task<IReadOnlyList<MinecraftInstallAddonVersionEntry>> GetVersionsAsync(
        MinecraftInstallAddonKind kind,
        string gameVersion,
        CancellationToken cancellationToken = default);
}

public sealed class MinecraftInstallAddonMetadataService : IMinecraftInstallAddonMetadataService
{
    private const string ModrinthApiRoot = "https://api.modrinth.com/v2/project/";
    private readonly HttpClient _httpClient;

    public MinecraftInstallAddonMetadataService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? PortableHttp.Client;
    }

    public async Task<IReadOnlyList<MinecraftInstallAddonVersionEntry>> GetVersionsAsync(
        MinecraftInstallAddonKind kind,
        string gameVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        (string project, string loader) = GetProject(kind);
        string gameVersions = Uri.EscapeDataString($"[\"{gameVersion}\"]");
        string loaders = Uri.EscapeDataString($"[\"{loader}\"]");
        string url = $"{ModrinthApiRoot}{project}/version?game_versions={gameVersions}&loaders={loaders}&featured=true";
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        ConfigureRequest(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        string json = await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
        JsonArray versions = JsonNode.Parse(json) as JsonArray
                             ?? throw new FormatException($"{kind} 版本列表不是数组。");

        List<MinecraftInstallAddonVersionEntry> result = [];
        foreach (JsonObject version in versions.OfType<JsonObject>())
        {
            JsonArray? files = version["files"] as JsonArray;
            JsonObject? file = files?.OfType<JsonObject>().FirstOrDefault(candidate =>
                                   bool.TryParse(candidate["primary"]?.ToString(), out bool primary) && primary)
                               ?? files?.OfType<JsonObject>().FirstOrDefault();
            string? versionNumber = version["version_number"]?.ToString();
            string? fileName = file?["filename"]?.ToString();
            string? downloadUrl = file?["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(versionNumber) || string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(downloadUrl))
                continue;

            _ = long.TryParse(file?["size"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long size);
            result.Add(new MinecraftInstallAddonVersionEntry(
                kind,
                versionNumber,
                fileName,
                downloadUrl,
                file?["hashes"]?["sha1"]?.ToString(),
                size,
                string.Equals(version["version_type"]?.ToString(), "release", StringComparison.OrdinalIgnoreCase)));
        }

        return result;
    }

    private static (string Project, string Loader) GetProject(MinecraftInstallAddonKind kind) =>
        kind switch
        {
            MinecraftInstallAddonKind.FabricApi => ("fabric-api", "fabric"),
            MinecraftInstallAddonKind.LegacyFabricApi => ("legacy-fabric-api", "fabric"),
            MinecraftInstallAddonKind.Qsl => ("qsl", "quilt"),
            MinecraftInstallAddonKind.OptiFabric => ("optifabric", "fabric"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static void ConfigureRequest(HttpRequestMessage request)
    {
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        request.Headers.Accept.ParseAdd("application/json");
        string language = CultureInfo.CurrentUICulture.Name;
        request.Headers.AcceptLanguage.ParseAdd(string.IsNullOrWhiteSpace(language) ? "zh-CN" : language);
    }
}
