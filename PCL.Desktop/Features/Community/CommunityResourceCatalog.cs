// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PCL.Desktop.Features.Community;

public enum CommunityResourceCategory
{
    Mod,
    Modpack,
    DataPack,
    ResourcePack,
    Shader,
    World
}

public enum CommunityResourceSort
{
    Relevance,
    Downloads,
    Updated
}

public enum CommunityResourceSource
{
    All,
    Modrinth,
    CurseForge
}

public sealed record CommunitySearchOptions(
    CommunityResourceSort Sort = CommunityResourceSort.Relevance,
    string? GameVersion = null,
    string? Loader = null,
    string? Tag = null,
    CommunityResourceSource Source = CommunityResourceSource.All);

public sealed record CommunityResourceEntry(
    string ProjectId,
    string Slug,
    string Title,
    string Description,
    string ProjectType,
    string? IconUrl,
    long Downloads,
    DateTimeOffset? UpdatedAt)
{
    public CommunityResourceSource Source { get; init; } = CommunityResourceSource.Modrinth;

    public string? ProjectUrl { get; init; }

    public string WebsiteUrl => ProjectUrl ?? (Source == CommunityResourceSource.CurseForge
        ? "https://www.curseforge.com/minecraft/" + CurseForgeProjectPath(ProjectType) + "/" +
          (string.IsNullOrWhiteSpace(Slug) ? ProjectId : Slug)
        : "https://modrinth.com/" + ProjectType + "/" + (string.IsNullOrWhiteSpace(Slug) ? ProjectId : Slug));

    private static string CurseForgeProjectPath(string projectType) => projectType.ToLowerInvariant() switch
    {
        "modpack" => "modpacks",
        "resourcepack" => "texture-packs",
        "shader" => "shaders",
        "datapack" => "data-packs",
        "world" => "worlds",
        _ => "mc-mods"
    };
}

public sealed record CommunityResourceDownloadFile(
    string FileName,
    string Url,
    long Size,
    string VersionId,
    string VersionName);

public sealed record CommunityResourceVersion(
    string VersionId,
    string Name,
    string VersionNumber,
    string? Changelog,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    IReadOnlyList<CommunityResourceDownloadFile> Files);

public sealed record CommunityResourceFileIdentity(
    string ProjectId,
    string ProjectSlug,
    string ProjectTitle,
    string ProjectType,
    string VersionId,
    string VersionNumber,
    DateTimeOffset? PublishedAt,
    string? IconUrl,
    string WebsiteUrl);

public sealed record CommunityResourceUpdateCandidate(
    CommunityResourceFileIdentity Current,
    CommunityResourceVersion Latest,
    CommunityResourceDownloadFile PrimaryFile);

public interface ICommunityResourceCatalog
{
    Task<IReadOnlyList<CommunityResourceEntry>> SearchAsync(
        CommunityResourceCategory category,
        string query,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<CommunityResourceDownloadFile?> ResolveDownloadAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityResourceVersion>> GetVersionsAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>WPF-style: identify a local jar/zip by SHA-1 against Modrinth version files.</summary>
    Task<CommunityResourceFileIdentity?> LookupFileBySha1Async(
        string sha1Hex,
        CancellationToken cancellationToken = default);

    /// <summary>Latest project version matching optional game/loader filters.</summary>
    Task<CommunityResourceVersion?> GetLatestVersionAsync(
        string projectId,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class ModrinthCommunityResourceCatalog : ICommunityResourceCatalog, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public ModrinthCommunityResourceCatalog()
        : this(CreateDefaultClient(), ownsClient: true)
    {
    }

    public ModrinthCommunityResourceCatalog(HttpClient client, bool ownsClient = false)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _ownsClient = ownsClient;
    }

    public async Task<IReadOnlyList<CommunityResourceEntry>> SearchAsync(
        CommunityResourceCategory category,
        string query,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CommunitySearchOptions();
        string facets = CreateFacets(category, options);
        string index = options.Sort switch
        {
            CommunityResourceSort.Downloads => "downloads",
            CommunityResourceSort.Updated => "updated",
            _ => "relevance"
        };
        string requestUrl = "https://api.modrinth.com/v2/search?limit=80&index=" + index +
                            "&query=" + Uri.EscapeDataString(query?.Trim() ?? string.Empty) +
                            "&facets=" + Uri.EscapeDataString(facets);
        using HttpResponseMessage response = await _client.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!TryGetProperty(document.RootElement, "hits", out JsonElement hits) || hits.ValueKind != JsonValueKind.Array)
            return [];

        List<CommunityResourceEntry> entries = [];
        foreach (JsonElement hit in hits.EnumerateArray())
        {
            if (hit.ValueKind != JsonValueKind.Object)
                continue;

            string projectId = ReadString(hit, "project_id");
            string slug = ReadString(hit, "slug");
            string title = ReadString(hit, "title");
            if (string.IsNullOrWhiteSpace(projectId) && string.IsNullOrWhiteSpace(slug))
                continue;
            if (string.IsNullOrWhiteSpace(title))
                title = slug;

            entries.Add(new CommunityResourceEntry(
                string.IsNullOrWhiteSpace(projectId) ? slug : projectId,
                slug,
                title,
                ReadString(hit, "description"),
                NormalizeProjectType(ReadString(hit, "project_type"), category),
                NullIfWhiteSpace(ReadString(hit, "icon_url")),
                ReadInt64(hit, "downloads"),
                ReadDateTimeOffset(hit, "date_modified")));
        }

        return entries;
    }

    public async Task<CommunityResourceDownloadFile?> ResolveDownloadAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        options ??= new CommunitySearchOptions();
        string id = string.IsNullOrWhiteSpace(entry.ProjectId) ? entry.Slug : entry.ProjectId;
        if (string.IsNullOrWhiteSpace(id))
            return null;

        List<string> query = ["limit=20"];
        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            query.Add("game_versions=" + Uri.EscapeDataString("[\"" + options.GameVersion.Trim() + "\"]"));
        if (!string.IsNullOrWhiteSpace(options.Loader) &&
            !string.Equals(options.Loader, "any", StringComparison.OrdinalIgnoreCase))
        {
            query.Add("loaders=" + Uri.EscapeDataString("[\"" + options.Loader.Trim().ToLowerInvariant() + "\"]"));
        }

        string requestUrl = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(id) +
                            "/version?" + string.Join('&', query);
        using HttpResponseMessage response = await _client.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return null;

        foreach (JsonElement version in document.RootElement.EnumerateArray())
        {
            if (version.ValueKind != JsonValueKind.Object)
                continue;
            if (!TryGetProperty(version, "files", out JsonElement files) || files.ValueKind != JsonValueKind.Array)
                continue;

            JsonElement? primary = null;
            foreach (JsonElement file in files.EnumerateArray())
            {
                if (file.ValueKind != JsonValueKind.Object)
                    continue;
                if (TryGetProperty(file, "primary", out JsonElement primaryFlag) &&
                    primaryFlag.ValueKind == JsonValueKind.True)
                {
                    primary = file;
                    break;
                }

                primary ??= file;
            }

            if (primary is not { } chosen)
                continue;

            string url = ReadString(chosen, "url");
            string fileName = ReadString(chosen, "filename");
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(fileName))
                continue;

            long size = 0;
            if (TryGetProperty(chosen, "size", out JsonElement sizeElement))
                sizeElement.TryGetInt64(out size);

            return new CommunityResourceDownloadFile(
                fileName,
                url,
                size,
                ReadString(version, "id"),
                ReadString(version, "name"));
        }

        return null;
    }

    public async Task<IReadOnlyList<CommunityResourceVersion>> GetVersionsAsync(
        CommunityResourceEntry entry,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        options ??= new CommunitySearchOptions();
        string id = string.IsNullOrWhiteSpace(entry.ProjectId) ? entry.Slug : entry.ProjectId;
        if (string.IsNullOrWhiteSpace(id))
            return [];

        // WPF CompFilesGet loads a large version list then groups client-side by game version.
        List<string> query = ["limit=100"];
        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            query.Add("game_versions=" + Uri.EscapeDataString("[\"" + options.GameVersion.Trim() + "\"]"));
        if (!string.IsNullOrWhiteSpace(options.Loader) &&
            !string.Equals(options.Loader, "any", StringComparison.OrdinalIgnoreCase))
        {
            query.Add("loaders=" + Uri.EscapeDataString("[\"" + options.Loader.Trim().ToLowerInvariant() + "\"]"));
        }

        string requestUrl = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(id) +
                            "/version?" + string.Join('&', query);
        using HttpResponseMessage response = await _client.GetAsync(requestUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        List<CommunityResourceVersion> versions = [];
        foreach (JsonElement version in document.RootElement.EnumerateArray())
        {
            if (version.ValueKind != JsonValueKind.Object)
                continue;

            List<CommunityResourceDownloadFile> files = [];
            if (TryGetProperty(version, "files", out JsonElement filesEl) && filesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement file in filesEl.EnumerateArray())
                {
                    if (file.ValueKind != JsonValueKind.Object)
                        continue;
                    string url = ReadString(file, "url");
                    string fileName = ReadString(file, "filename");
                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(fileName))
                        continue;
                    long size = 0;
                    if (TryGetProperty(file, "size", out JsonElement sizeEl))
                        sizeEl.TryGetInt64(out size);
                    files.Add(new CommunityResourceDownloadFile(
                        fileName,
                        url,
                        size,
                        ReadString(version, "id"),
                        ReadString(version, "name")));
                }
            }

            if (files.Count == 0)
                continue;

            versions.Add(new CommunityResourceVersion(
                ReadString(version, "id"),
                ReadString(version, "name"),
                ReadString(version, "version_number"),
                NullIfWhiteSpace(ReadString(version, "changelog")),
                ReadDateTimeOffset(version, "date_published"),
                ReadStringArray(version, "game_versions"),
                ReadStringArray(version, "loaders"),
                files));
        }

        return versions;
    }

    public async Task<CommunityResourceFileIdentity?> LookupFileBySha1Async(
        string sha1Hex,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sha1Hex) || sha1Hex.Length is not (40 or 64))
            return null;

        string algorithm = sha1Hex.Length == 64 ? "sha512" : "sha1";
        string url = "https://api.modrinth.com/v2/version_file/" + Uri.EscapeDataString(sha1Hex.ToLowerInvariant()) +
                     "?algorithm=" + algorithm;
        try
        {
            using HttpResponseMessage response = await _client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            JsonElement root = document.RootElement;
            string projectId = ReadString(root, "project_id");
            string versionId = ReadString(root, "id");
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(versionId))
                return null;

            string versionNumber = ReadString(root, "version_number");
            DateTimeOffset? published = ReadDateTimeOffset(root, "date_published");

            // Project metadata for title / icon / type.
            string title = projectId;
            string slug = projectId;
            string projectType = "mod";
            string? iconUrl = null;
            try
            {
                string projectUrl = "https://api.modrinth.com/v2/project/" + Uri.EscapeDataString(projectId);
                using HttpResponseMessage projectResponse = await _client.GetAsync(projectUrl, cancellationToken)
                    .ConfigureAwait(false);
                if (projectResponse.IsSuccessStatusCode)
                {
                    await using Stream projectStream =
                        await projectResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using JsonDocument projectDoc =
                        await JsonDocument.ParseAsync(projectStream, cancellationToken: cancellationToken)
                            .ConfigureAwait(false);
                    title = NullIfWhiteSpace(ReadString(projectDoc.RootElement, "title")) ?? title;
                    slug = NullIfWhiteSpace(ReadString(projectDoc.RootElement, "slug")) ?? slug;
                    projectType = NullIfWhiteSpace(ReadString(projectDoc.RootElement, "project_type")) ?? projectType;
                    iconUrl = NullIfWhiteSpace(ReadString(projectDoc.RootElement, "icon_url"));
                }
            }
            catch
            {
                // identity still useful without project decoration
            }

            string website = "https://modrinth.com/" + projectType + "/" +
                             (string.IsNullOrWhiteSpace(slug) ? projectId : slug);
            return new CommunityResourceFileIdentity(
                projectId,
                slug,
                title,
                projectType,
                versionId,
                versionNumber,
                published,
                iconUrl,
                website);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            return null;
        }
    }

    public async Task<CommunityResourceVersion?> GetLatestVersionAsync(
        string projectId,
        CommunitySearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            return null;

        options ??= new CommunitySearchOptions();
        CommunityResourceEntry stub = new(
            projectId,
            projectId,
            projectId,
            string.Empty,
            "mod",
            null,
            0,
            null);
        IReadOnlyList<CommunityResourceVersion> versions =
            await GetVersionsAsync(stub, options, cancellationToken).ConfigureAwait(false);
        return versions
            .OrderByDescending(static v => v.PublishedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private static HttpClient CreateDefaultClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        return client;
    }

    private static string CreateFacets(CommunityResourceCategory category, CommunitySearchOptions options)
    {
        List<string> groups =
        [
            category switch
            {
                CommunityResourceCategory.Mod => "[\"project_type:mod\"]",
                CommunityResourceCategory.Modpack => "[\"project_type:modpack\"]",
                CommunityResourceCategory.DataPack => "[\"project_type:datapack\"]",
                CommunityResourceCategory.ResourcePack => "[\"project_type:resourcepack\"]",
                CommunityResourceCategory.Shader => "[\"project_type:shader\"]",
                CommunityResourceCategory.World => "[\"project_type:world\"]",
                _ => "[\"project_type:mod\"]"
            }
        ];

        if (!string.IsNullOrWhiteSpace(options.GameVersion))
            groups.Add("[\"versions:" + EscapeFacetValue(options.GameVersion.Trim()) + "\"]");

        if (!string.IsNullOrWhiteSpace(options.Loader) &&
            !string.Equals(options.Loader, "any", StringComparison.OrdinalIgnoreCase))
        {
            groups.Add("[\"categories:" + EscapeFacetValue(options.Loader.Trim().ToLowerInvariant()) + "\"]");
        }

        if (!string.IsNullOrWhiteSpace(options.Tag))
            groups.Add("[\"categories:" + EscapeFacetValue(options.Tag.Trim().ToLowerInvariant()) + "\"]");

        return "[" + string.Join(',', groups) + "]";
    }

    private static string EscapeFacetValue(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string NormalizeProjectType(string projectType, CommunityResourceCategory category)
    {
        if (!string.IsNullOrWhiteSpace(projectType))
            return projectType.Trim();

        return category switch
        {
            CommunityResourceCategory.Modpack => "modpack",
            CommunityResourceCategory.ResourcePack => "resourcepack",
            CommunityResourceCategory.Shader => "shader",
            CommunityResourceCategory.DataPack => "datapack",
            CommunityResourceCategory.World => "world",
            _ => "mod"
        };
    }

    private static string ReadString(JsonElement element, string name) =>
        TryGetProperty(element, name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long ReadInt64(JsonElement element, string name) =>
        TryGetProperty(element, name, out JsonElement value) && value.TryGetInt64(out long result) ? result : 0L;

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement element, string name) =>
        DateTimeOffset.TryParse(ReadString(element, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value)
            ? value
            : null;

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string> ReadStringArray(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            return [];

        List<string> items = [];
        foreach (JsonElement item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    items.Add(s);
            }
        }

        return items;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
