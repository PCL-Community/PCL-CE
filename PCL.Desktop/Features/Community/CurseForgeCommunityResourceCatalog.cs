// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PCL.Desktop.Features.Community;

public sealed class CurseForgeCommunityResourceCatalog : ICommunityResourceCatalog, IDisposable
{
    private const string ApiRoot = "https://api.curseforge.com/v1";
    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly string? _apiKey;

    public CurseForgeCommunityResourceCatalog()
        : this(CreateDefaultClient(), ResolveApiKey(), ownsClient: true)
    {
    }

    public CurseForgeCommunityResourceCatalog(HttpClient client, string? apiKey, bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        _ownsClient = ownsClient;
    }

    public async Task<IReadOnlyList<CommunityResourceEntry>> SearchAsync(
        CommunityResourceCategory category,
        string query,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CommunitySearchOptions();
        List<string> parameters =
        [
            "gameId=432",
            "sortOrder=desc",
            "pageSize=80",
            "classId=" + GetClassId(category).ToString(CultureInfo.InvariantCulture),
            "sortField=" + GetSortField(options.Sort).ToString(CultureInfo.InvariantCulture)
        ];
        if (!string.IsNullOrWhiteSpace(query))
            parameters.Add("searchFilter=" + Uri.EscapeDataString(query.Trim()));
        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            parameters.Add("gameVersion=" + Uri.EscapeDataString(options.GameVersion.Trim()));
        if (TryGetLoaderType(options.Loader, out int loaderType))
            parameters.Add("modLoaderType=" + loaderType.ToString(CultureInfo.InvariantCulture));

        using HttpResponseMessage response = await SendAsync(
            HttpMethod.Get,
            ApiRoot + "/mods/search?" + string.Join('&', parameters),
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!TryGetProperty(document.RootElement, "data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
            return [];

        List<CommunityResourceEntry> entries = [];
        foreach (JsonElement project in data.EnumerateArray())
        {
            string id = ReadNumberOrString(project, "id");
            string slug = ReadString(project, "slug");
            string title = ReadString(project, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                continue;

            string? iconUrl = null;
            if (TryGetProperty(project, "logo", out JsonElement logo) && logo.ValueKind == JsonValueKind.Object)
                iconUrl = NullIfWhiteSpace(ReadString(logo, "thumbnailUrl")) ?? NullIfWhiteSpace(ReadString(logo, "url"));
            string? website = null;
            if (TryGetProperty(project, "links", out JsonElement links) && links.ValueKind == JsonValueKind.Object)
                website = NullIfWhiteSpace(ReadString(links, "websiteUrl"));

            entries.Add(new CommunityResourceEntry(
                id,
                slug,
                title,
                ReadString(project, "summary"),
                GetProjectType(category),
                iconUrl,
                ReadInt64(project, "downloadCount"),
                ReadDateTimeOffset(project, "dateModified"))
            {
                Source = CommunityResourceSource.CurseForge,
                ProjectUrl = website
            });
        }

        return entries;
    }

    public async Task<CommunityResourceDownloadFile?> ResolveDownloadAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CommunityResourceVersion> versions =
            await GetVersionsAsync(entry, options, cancellationToken).ConfigureAwait(false);
        return versions.SelectMany(static version => version.Files).FirstOrDefault();
    }

    public async Task<IReadOnlyList<CommunityResourceVersion>> GetVersionsAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        options ??= new CommunitySearchOptions();
        if (string.IsNullOrWhiteSpace(entry.ProjectId))
            return [];

        List<string> parameters = ["pageSize=10000"];
        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            parameters.Add("gameVersion=" + Uri.EscapeDataString(options.GameVersion.Trim()));
        if (TryGetLoaderType(options.Loader, out int loaderType))
            parameters.Add("modLoaderType=" + loaderType.ToString(CultureInfo.InvariantCulture));

        string url = ApiRoot + "/mods/" + Uri.EscapeDataString(entry.ProjectId) + "/files?" + string.Join('&', parameters);
        using HttpResponseMessage response = await SendAsync(HttpMethod.Get, url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!TryGetProperty(document.RootElement, "data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
            return [];

        List<CommunityResourceVersion> versions = [];
        foreach (JsonElement file in data.EnumerateArray())
        {
            string id = ReadNumberOrString(file, "id");
            string fileName = ReadString(file, "fileName");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(fileName))
                continue;

            string urlValue = ReadString(file, "downloadUrl");
            if (string.IsNullOrWhiteSpace(urlValue))
                urlValue = CreateForgeCdnUrl(id, fileName);
            urlValue = NormalizeDownloadUrl(urlValue);

            string displayName = NullIfWhiteSpace(ReadString(file, "displayName")) ?? fileName;
            List<string> gameVersions = ReadStringArray(file, "gameVersions");
            List<string> loaders = gameVersions
                .Where(static value => value.Equals("Forge", StringComparison.OrdinalIgnoreCase) ||
                                       value.Equals("NeoForge", StringComparison.OrdinalIgnoreCase) ||
                                       value.Equals("Fabric", StringComparison.OrdinalIgnoreCase) ||
                                       value.Equals("Quilt", StringComparison.OrdinalIgnoreCase) ||
                                       value.Equals("LiteLoader", StringComparison.OrdinalIgnoreCase))
                .Select(static value => value.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> minecraftVersions = gameVersions
                .Where(static value => char.IsDigit(value.FirstOrDefault()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            CommunityResourceDownloadFile download = new(
                fileName,
                urlValue,
                ReadInt64(file, "fileLength"),
                id,
                displayName);
            versions.Add(new CommunityResourceVersion(
                id,
                displayName,
                displayName,
                NullIfWhiteSpace(ReadString(file, "changelog")),
                ReadDateTimeOffset(file, "fileDate"),
                minecraftVersions,
                loaders,
                [download]));
        }

        return versions
            .OrderByDescending(static version => version.PublishedAt ?? DateTimeOffset.MinValue)
            .ToArray();
    }

    public Task<CommunityResourceFileIdentity?> LookupFileBySha1Async(
        string sha1Hex,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<CommunityResourceFileIdentity?>(null);

    public async Task<CommunityResourceVersion?> GetLatestVersionAsync(
        string projectId,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        CommunityResourceEntry entry = new(projectId, projectId, projectId, string.Empty, "mod", null, 0L, null)
        {
            Source = CommunityResourceSource.CurseForge
        };
        IReadOnlyList<CommunityResourceVersion> versions = await GetVersionsAsync(entry, options, cancellationToken)
            .ConfigureAwait(false);
        return versions.Count == 0 ? null : versions[0];
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("CurseForge API 密钥未配置，请设置 PCL_CURSEFORGE_API_KEY。");

        using HttpRequestMessage request = new(method, url);
        request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
        return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
    }

    private static HttpClient CreateDefaultClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        return client;
    }

    private static string? ResolveApiKey() =>
        NullIfWhiteSpace(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY")) ??
        NullIfWhiteSpace(Environment.GetEnvironmentVariable("CURSEFORGE_API_KEY"));

    private static int GetClassId(CommunityResourceCategory category) => category switch
    {
        CommunityResourceCategory.Mod => 6,
        CommunityResourceCategory.Modpack => 4471,
        CommunityResourceCategory.DataPack => 6945,
        CommunityResourceCategory.ResourcePack => 12,
        CommunityResourceCategory.Shader => 6552,
        CommunityResourceCategory.World => 17,
        _ => 6
    };

    private static int GetSortField(CommunityResourceSort sort) => sort switch
    {
        CommunityResourceSort.Downloads => 6,
        CommunityResourceSort.Updated => 3,
        _ => 4
    };

    private static bool TryGetLoaderType(string? loader, out int type)
    {
        type = loader?.Trim().ToLowerInvariant() switch
        {
            "forge" => 1,
            "fabric" => 4,
            "quilt" => 5,
            "neoforge" => 6,
            _ => 0
        };
        return type != 0;
    }

    private static string GetProjectType(CommunityResourceCategory category) => category switch
    {
        CommunityResourceCategory.Modpack => "modpack",
        CommunityResourceCategory.DataPack => "datapack",
        CommunityResourceCategory.ResourcePack => "resourcepack",
        CommunityResourceCategory.Shader => "shader",
        CommunityResourceCategory.World => "world",
        _ => "mod"
    };

    private static string CreateForgeCdnUrl(string id, string fileName)
    {
        if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long numericId))
            throw new InvalidDataException("CurseForge 返回的文件 ID 无效。");
        long group = numericId / 1000L;
        long suffix = numericId % 1000L;
        return $"https://edge.forgecdn.net/files/{group.ToString(CultureInfo.InvariantCulture)}/{suffix.ToString("000", CultureInfo.InvariantCulture)}/{Uri.EscapeDataString(fileName)}";
    }

    private static string NormalizeDownloadUrl(string url) => url
        .Replace("-service.overwolf.wtf", ".forgecdn.net", StringComparison.OrdinalIgnoreCase)
        .Replace("://mediafilez.", "://edge.", StringComparison.OrdinalIgnoreCase)
        .Replace("://media.", "://edge.", StringComparison.OrdinalIgnoreCase)
        .Replace(" ", "%20", StringComparison.Ordinal);

    private static string ReadString(JsonElement element, string name) =>
        TryGetProperty(element, name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string ReadNumberOrString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out JsonElement value))
            return string.Empty;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
    }

    private static long ReadInt64(JsonElement element, string name) =>
        TryGetProperty(element, name, out JsonElement value) && value.TryGetInt64(out long result) ? result : 0L;

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string name) =>
        DateTimeOffset.TryParse(ReadString(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value)
            ? value
            : null;

    private static List<string> ReadStringArray(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            return [];
        return array.EnumerateArray()
            .Where(static value => value.ValueKind == JsonValueKind.String)
            .Select(static value => value.GetString())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!)
            .ToList();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value))
            return true;
        value = default;
        return false;
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
