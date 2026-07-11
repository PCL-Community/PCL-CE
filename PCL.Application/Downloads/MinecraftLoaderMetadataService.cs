// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PCL.Core.IO.Net;

namespace PCL.Application.Downloads;

public enum MinecraftLoaderKind
{
    Forge,
    Cleanroom,
    NeoForge,
    Fabric,
    LegacyFabric,
    Quilt,
    LabyMod,
    OptiFine,
    LiteLoader
}

public sealed record MinecraftLoaderInstallRequest(MinecraftLoaderKind Kind, string LoaderVersion);

public sealed record MinecraftLoaderVersionEntry(MinecraftLoaderKind Kind, string Version, bool Stable)
{
    public string DisplayVersion
    {
        get
        {
            if (Kind == MinecraftLoaderKind.LabyMod)
            {
                string[] parts = Version.Split('+', 3, StringSplitOptions.TrimEntries);
                if (parts.Length == 3)
                    return parts[1];
            }

            return Version.Replace("+build", string.Empty, StringComparison.Ordinal);
        }
    }
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

    Task<JsonObject> GetLoaderVersionProfileAsync(
        MinecraftLoaderInstallRequest request,
        string gameVersion,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{request.Kind} 不提供可直接使用的版本描述文件。");
}

public sealed class MinecraftLoaderMetadataService : IMinecraftLoaderMetadataService
{
    private const string FabricMetadataRoot = "https://meta.fabricmc.net/v2/versions/loader/";
    private const string FabricMavenRoot = "https://maven.fabricmc.net/";
    private const string LegacyFabricMetadataRoot = "https://meta.legacyfabric.net/v2/versions/loader/";
    private const string LegacyFabricMavenRoot = "https://maven.legacyfabric.net/";
    private const string QuiltMetadataRoot = "https://meta.quiltmc.org/v3/versions/loader/";
    private const string QuiltMavenRoot = "https://maven.quiltmc.org/repository/release/";
    private const string ForgeMetadataUrl = "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
    private const string NeoForgeMetadataUrl = "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
    private const string NeoForgeLegacyMetadataUrl = "https://maven.neoforged.net/releases/net/neoforged/forge/maven-metadata.xml";
    private const string CleanroomReleasesUrl = "https://api.github.com/repos/CleanroomMC/Cleanroom/releases?per_page=100";
    private const string LiteLoaderVersionsUrl = "https://dl.liteloader.com/versions/versions.json";
    private const string OptiFineVersionsUrl = "https://optifine.net/downloads";
    private const string LabyModManifestRoot = "https://releases.r2.labymod.net/api/v1/manifest/";
    private const string LabyModDownloadManifestRoot = "https://releases.r2.labymod.net/api/v1/download/manifest/labymod4/";

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
        if (kind is not (MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric or MinecraftLoaderKind.Quilt))
            return await GetSpecialLoaderVersionsAsync(kind, gameVersion, cancellationToken).ConfigureAwait(false);

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

        if (request.Kind is not (MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric or MinecraftLoaderKind.Quilt))
            throw new NotSupportedException($"{request.Kind} 需要安装器流程，不能按 Fabric/Quilt 元数据安装。");

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

    public async Task<JsonObject> GetLoaderVersionProfileAsync(
        MinecraftLoaderInstallRequest request,
        string gameVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);

        return request.Kind switch
        {
            MinecraftLoaderKind.LabyMod => await GetLabyModProfileAsync(request.LoaderVersion, gameVersion, cancellationToken)
                .ConfigureAwait(false),
            MinecraftLoaderKind.LiteLoader => await GetLiteLoaderProfileAsync(request.LoaderVersion, gameVersion, cancellationToken)
                .ConfigureAwait(false),
            _ => throw new NotSupportedException($"{request.Kind} 不提供可直接使用的版本描述文件。")
        };
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
        libraries.Add(new MinecraftLoaderLibrary(loaderMaven, GetLoaderMavenRoot(kind)));

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
        return (RequiredString(intermediary, "maven"),
            kind == MinecraftLoaderKind.LegacyFabric ? LegacyFabricMavenRoot : FabricMavenRoot);
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
            MinecraftLoaderKind.LegacyFabric => LegacyFabricMetadataRoot,
            MinecraftLoaderKind.Quilt => QuiltMetadataRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string GetLoaderMavenRoot(MinecraftLoaderKind kind) =>
        kind switch
        {
            MinecraftLoaderKind.Fabric or MinecraftLoaderKind.LegacyFabric => FabricMavenRoot,
            MinecraftLoaderKind.Quilt => QuiltMavenRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetSpecialLoaderVersionsAsync(
        MinecraftLoaderKind kind,
        string gameVersion,
        CancellationToken cancellationToken) =>
        kind switch
        {
            MinecraftLoaderKind.Forge => GetForgeVersionsAsync(gameVersion, cancellationToken),
            MinecraftLoaderKind.NeoForge => GetNeoForgeVersionsAsync(gameVersion, cancellationToken),
            MinecraftLoaderKind.Cleanroom => GetCleanroomVersionsAsync(gameVersion, cancellationToken),
            MinecraftLoaderKind.LiteLoader => GetLiteLoaderVersionsAsync(gameVersion, cancellationToken),
            MinecraftLoaderKind.OptiFine => GetOptiFineVersionsAsync(gameVersion, cancellationToken),
            MinecraftLoaderKind.LabyMod => GetLabyModVersionsAsync(gameVersion, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetForgeVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        string xml = await GetStringAsync(ForgeMetadataUrl, cancellationToken).ConfigureAwait(false);
        string prefix = NormalizeGameVersion(gameVersion).Replace("-", "_", StringComparison.Ordinal) + "-";
        return ReadMavenVersions(xml)
            .Where(version => version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(version => version[prefix.Length..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(version => new MinecraftLoaderVersionEntry(MinecraftLoaderKind.Forge, version, IsReleaseVersion(version)))
            .ToArray();
    }

    private async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetNeoForgeVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        List<string> versions = [];
        if (string.Equals(gameVersion, "1.20.1", StringComparison.OrdinalIgnoreCase))
        {
            string legacyXml = await GetStringAsync(NeoForgeLegacyMetadataUrl, cancellationToken).ConfigureAwait(false);
            versions.AddRange(ReadMavenVersions(legacyXml).Where(version => version.StartsWith("1.20.1-", StringComparison.Ordinal)));
        }
        else
        {
            string xml = await GetStringAsync(NeoForgeMetadataUrl, cancellationToken).ConfigureAwait(false);
            versions.AddRange(ReadMavenVersions(xml).Where(version =>
                string.Equals(GetNeoForgeGameVersion(version), gameVersion, StringComparison.OrdinalIgnoreCase)));
        }

        return versions
            .AsEnumerable()
            .Reverse()
            .Where(static version => !string.Equals(version, "1.20.1-47.1.82", StringComparison.Ordinal))
            .Select(version => new MinecraftLoaderVersionEntry(
                MinecraftLoaderKind.NeoForge,
                version.StartsWith("1.20.1-", StringComparison.Ordinal) ? version[7..] : version,
                IsReleaseVersion(version)))
            .ToArray();
    }

    private async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetCleanroomVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(gameVersion, "1.12.2", StringComparison.OrdinalIgnoreCase))
            return [];

        string json = await GetStringAsync(CleanroomReleasesUrl, cancellationToken).ConfigureAwait(false);
        JsonArray releases = JsonNode.Parse(json) as JsonArray
                             ?? throw new FormatException("Cleanroom 发布列表不是数组。");
        return releases
            .OfType<JsonObject>()
            .Select(release => release["tag_name"]?.ToString())
            .Where(static version => !string.IsNullOrWhiteSpace(version))
            .Select(version => new MinecraftLoaderVersionEntry(
                MinecraftLoaderKind.Cleanroom,
                version!,
                IsReleaseVersion(version!)))
            .ToArray();
    }

    private async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLiteLoaderVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        string json = await GetStringAsync(LiteLoaderVersionsUrl, cancellationToken).ConfigureAwait(false);
        JsonObject root = JsonNode.Parse(json) as JsonObject
                          ?? throw new FormatException("LiteLoader 版本列表不是对象。");
        JsonObject? game = root["versions"]?[gameVersion] as JsonObject;
        JsonObject? channel = game?["artefacts"] as JsonObject ?? game?["snapshots"] as JsonObject;
        JsonObject? latest = channel?["com.mumfrey:liteloader"]?["latest"] as JsonObject;
        string? version = latest?["version"]?.ToString();
        if (string.IsNullOrWhiteSpace(version))
            return [];

        bool stable = !string.Equals(latest?["stream"]?.ToString(), "SNAPSHOT", StringComparison.OrdinalIgnoreCase);
        return [new MinecraftLoaderVersionEntry(MinecraftLoaderKind.LiteLoader, version, stable)];
    }

    private async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetOptiFineVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        string html = await GetStringAsync(OptiFineVersionsUrl, cancellationToken).ConfigureAwait(false);
        MatchCollection matches = Regex.Matches(
            html,
            "OptiFine_(?<version>[0-9A-Za-z_.]+)\\.jar",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        string prefix = gameVersion + "_";
        return matches.Cast<Match>()
            .Select(match => match.Groups["version"].Value)
            .Where(version => version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(version => new MinecraftLoaderVersionEntry(MinecraftLoaderKind.OptiFine, version, IsReleaseVersion(version)))
            .ToArray();
    }

    private async Task<IReadOnlyList<MinecraftLoaderVersionEntry>> GetLabyModVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        List<MinecraftLoaderVersionEntry> result = [];
        foreach ((string channel, bool stable) in new[] { ("production", true), ("snapshot", false) })
        {
            string json = await GetStringAsync(LabyModManifestRoot + channel + "/latest.json", cancellationToken)
                .ConfigureAwait(false);
            JsonObject manifest = JsonNode.Parse(json) as JsonObject
                                  ?? throw new FormatException($"LabyMod {channel} 清单不是对象。");
            bool supportsGame = manifest["minecraftVersions"] is JsonArray games && games
                .OfType<JsonObject>()
                .Any(game => string.Equals(game["version"]?.ToString(), gameVersion, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(game["tag"]?.ToString(), gameVersion, StringComparison.OrdinalIgnoreCase));
            if (!supportsGame)
                continue;

            string? version = manifest["labyModVersion"]?.ToString();
            string? commit = manifest["commitReference"]?.ToString();
            if (!string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(commit))
                result.Add(new MinecraftLoaderVersionEntry(MinecraftLoaderKind.LabyMod, $"{channel}+{version}+{commit}", stable));
        }

        return result;
    }

    private async Task<JsonObject> GetLabyModProfileAsync(
        string loaderVersion,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        string[] parts = loaderVersion.Split('+', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || parts.Any(string.IsNullOrWhiteSpace))
            throw new FormatException("LabyMod 版本标识无效。");

        string url = $"{LabyModDownloadManifestRoot}{Uri.EscapeDataString(parts[0])}/" +
                     $"{Uri.EscapeDataString(gameVersion)}/{Uri.EscapeDataString(parts[2])}.json";
        string json = await GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        return JsonNode.Parse(json) as JsonObject
               ?? throw new FormatException("LabyMod 版本描述不是对象。");
    }

    private async Task<JsonObject> GetLiteLoaderProfileAsync(
        string loaderVersion,
        string gameVersion,
        CancellationToken cancellationToken)
    {
        string json = await GetStringAsync(LiteLoaderVersionsUrl, cancellationToken).ConfigureAwait(false);
        JsonObject root = JsonNode.Parse(json) as JsonObject
                          ?? throw new FormatException("LiteLoader 版本列表不是对象。");
        JsonObject? game = root["versions"]?[gameVersion] as JsonObject;
        JsonObject? channel = game?["artefacts"] as JsonObject ?? game?["snapshots"] as JsonObject;
        JsonObject? latest = channel?["com.mumfrey:liteloader"]?["latest"] as JsonObject;
        if (latest is null || !string.Equals(latest["version"]?.ToString(), loaderVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"未找到 LiteLoader {loaderVersion} 对 Minecraft {gameVersion} 的安装元数据。");

        JsonArray libraries = latest["libraries"]?.DeepClone() as JsonArray ?? [];
        libraries.Add((JsonNode)new JsonObject
        {
            ["name"] = "com.mumfrey:liteloader:" + loaderVersion,
            ["url"] = "https://dl.liteloader.com/versions/"
        });
        string now = DateTimeOffset.UtcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        return new JsonObject
        {
            ["id"] = gameVersion + "-LiteLoader",
            ["inheritsFrom"] = gameVersion,
            ["jar"] = gameVersion,
            ["time"] = now,
            ["releaseTime"] = now,
            ["type"] = "release",
            ["arguments"] = new JsonObject
            {
                ["game"] = new JsonArray("--tweakClass", latest["tweakClass"]?.ToString() ?? "com.mumfrey.liteloader.launch.LiteLoaderTweaker")
            },
            ["libraries"] = libraries,
            ["mainClass"] = "net.minecraft.launchwrapper.Launch",
            ["minimumLauncherVersion"] = 18
        };
    }

    private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        ConfigureRequest(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> ReadMavenVersions(string xml)
    {
        XDocument document = XDocument.Parse(xml, LoadOptions.None);
        return document.Descendants("version")
            .Select(static element => element.Value.Trim())
            .Where(static version => !string.IsNullOrWhiteSpace(version));
    }

    private static string? GetNeoForgeGameVersion(string version)
    {
        string numeric = version.Split('-', 2)[0];
        string[] pieces = numeric.Split('.');
        if (pieces.Length < 2 || !int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int major))
            return null;

        if (major == 0 && pieces.Length >= 2)
            return pieces[1];
        return major >= 24
            ? $"{major}.{pieces[1]}" + (pieces.Length > 2 && pieces[2] != "0" ? "." + pieces[2] : string.Empty)
            : $"1.{major}" + (pieces[1] != "0" ? "." + pieces[1] : string.Empty);
    }

    private static bool IsReleaseVersion(string version) =>
        !version.Contains("alpha", StringComparison.OrdinalIgnoreCase) &&
        !version.Contains("beta", StringComparison.OrdinalIgnoreCase) &&
        !version.Contains("snapshot", StringComparison.OrdinalIgnoreCase) &&
        !version.Contains("pre", StringComparison.OrdinalIgnoreCase) &&
        !version.Contains("rc", StringComparison.OrdinalIgnoreCase);

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
