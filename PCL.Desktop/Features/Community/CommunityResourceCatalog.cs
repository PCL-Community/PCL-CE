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
    public string WebsiteUrl => "https://modrinth.com/" + ProjectType + "/" + Slug;
}

public interface ICommunityResourceCatalog
{
    Task<IReadOnlyList<CommunityResourceEntry>> SearchAsync(
        CommunityResourceCategory category,
        string query,
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
        CancellationToken cancellationToken = default)
    {
        string facets = CreateFacets(category);
        string requestUrl = "https://api.modrinth.com/v2/search?limit=30&index=relevance&query=" +
                            Uri.EscapeDataString(query?.Trim() ?? string.Empty) +
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
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(title))
                continue;

            entries.Add(new CommunityResourceEntry(
                projectId,
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

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private static HttpClient CreateDefaultClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PCL-N", "1.0"));
        return client;
    }

    private static string CreateFacets(CommunityResourceCategory category) =>
        category switch
        {
            CommunityResourceCategory.Mod => "[[\"project_type:mod\"]]",
            CommunityResourceCategory.Modpack => "[[\"project_type:modpack\"]]",
            CommunityResourceCategory.DataPack => "[[\"all_project_types:datapack\"]]",
            CommunityResourceCategory.ResourcePack => "[[\"project_type:resourcepack\"]]",
            CommunityResourceCategory.Shader => "[[\"project_type:shader\"]]",
            CommunityResourceCategory.World => "[[\"project_type:mod\"],[\"categories:worldgen\"]]",
            _ => "[]"
        };

    private static string NormalizeProjectType(string projectType, CommunityResourceCategory category)
    {
        if (!string.IsNullOrWhiteSpace(projectType))
            return projectType.Trim();

        return category switch
        {
            CommunityResourceCategory.Modpack => "modpack",
            CommunityResourceCategory.ResourcePack => "resourcepack",
            CommunityResourceCategory.Shader => "shader",
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
