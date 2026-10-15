using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.ResourceProject.Comp.Abstractions;
using PCL.Core.Minecraft.ResourceProject.Comp.Converters;
using PCL.Core.Minecraft.ResourceProject.Comp.Infrastructure;
using PCL.Core.Minecraft.ResourceProject.Comp.Models;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Clients;

public sealed class ModrinthClient : ICompClient
{
    private const string BaseUrl = "https://api.modrinth.com/v2";

    private readonly string? _accessToken;
    private readonly HttpClient? _httpClient;
    private readonly RateLimiter _rateLimiter;

    public ModrinthClient(string? accessToken = null, HttpClient? httpClient = null)
    {
        _accessToken = accessToken;
        _httpClient = httpClient;
        _rateLimiter = new RateLimiter(300);
    }

    public async Task<CompSearchResult> SearchProjects(CompSearchFilter filter, CancellationToken ct = default)
    {
        var url = _BuildSearchUrl(filter);
        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        return ModrinthModelConverter.ToSearchResult(root, filter.Offset, filter.Limit);
    }

    public async Task<CompProject> GetProject(string projectId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/project/{projectId}";
        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        return ModrinthModelConverter.ToProject(root);
    }

    public async Task<List<CompProject>> GetProjects(IEnumerable<string> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.ToList();
        if (ids.Count == 0) return [];

        var idsJson = JsonSerializer.Serialize(ids, JsonCompat.SerializerOptions);
        var url = $"{BaseUrl}/projects?ids={Uri.EscapeDataString(idsJson)}";
        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);

        var results = new List<CompProject>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                results.Add(ModrinthModelConverter.ToProject(item));
        }
        return results;
    }

    public async Task<List<CompProject>> GetFeaturedProjects(string? gameVersion = null, CancellationToken ct = default)
    {
        var filter = new CompSearchFilter
        {
            SortField = CompSortField.Downloads,
            SortOrder = SortOrder.Desc,
            Limit = 20,
            GameVersion = gameVersion
        };
        var result = await SearchProjects(filter, ct).ConfigureAwait(false);
        return result.Hits;
    }

    public async Task<string> GetProjectDescription(string projectId, CancellationToken ct = default)
    {
        var project = await GetProject(projectId, ct).ConfigureAwait(false);
        return project.DescriptionHtml ?? project.Summary;
    }

    public async Task<List<CompFile>> GetProjectFiles(string projectId, CompSearchFilter? filter = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/project/{projectId}/version";
        if (filter is not null)
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(filter.GameVersion))
                queryParams.Add($"game_versions=[\"{filter.GameVersion}\"]");
            if (filter.Loaders.Count > 0)
                queryParams.Add($"loaders=[{string.Join(",", filter.Loaders.Select(l => $"\"{_LoaderToString(l)}\""))}]");
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);
        }

        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        var results = new List<CompFile>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                results.Add(ModrinthModelConverter.ToFile(item));
        }
        return results;
    }

    public async Task<CompFile> GetFile(string fileId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/version/{fileId}";
        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        return ModrinthModelConverter.ToFile(root);
    }

    public async Task<string> GetFileDownloadUrl(string fileId, CancellationToken ct = default)
    {
        var file = await GetFile(fileId, ct).ConfigureAwait(false);
        return file.DownloadUrl?.ToString() ?? "";
    }

    public async Task<string> GetFileChangelog(string fileId, CancellationToken ct = default)
    {
        var file = await GetFile(fileId, ct).ConfigureAwait(false);
        return file.Changelog ?? "";
    }

    public async Task<Dictionary<string, List<CompFile>>> MatchFingerprints(
        IEnumerable<string> hashes, HashAlgorithm algo, CancellationToken ct = default)
    {
        var hashList = hashes.ToList();
        if (hashList.Count == 0) return new Dictionary<string, List<CompFile>>();

        var algoStr = algo switch
        {
            HashAlgorithm.Sha1 => "sha1",
            HashAlgorithm.Sha512 => "sha512",
            HashAlgorithm.Md5 => "md5",
            _ => "sha1"
        };

        var body = JsonSerializer.Serialize(
            new { hashes = hashList, algorithm = algoStr },
            JsonCompat.SerializerOptions);

        await _rateLimiter.WaitIfNeeded(ct).ConfigureAwait(false);

        using var request = HttpRequest.CreatePost($"{BaseUrl}/version_files/from_hashes")
            .WithContent(body, "application/json")
            .ApplyModrinthAuth(_accessToken);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);
        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var root = JsonSerializer.Deserialize<JsonElement>(
            await response.AsStringAsync(ct).ConfigureAwait(false), JsonCompat.SerializerOptions);

        var result = new Dictionary<string, List<CompFile>>();
        foreach (var entry in root.EnumerateObject())
        {
            var files = new List<CompFile>();
            if (entry.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in entry.Value.EnumerateArray())
                    files.Add(ModrinthModelConverter.ToFile(item));
            }
            result[entry.Name] = files;
        }
        return result;
    }

    public async Task<CompFile?> CheckForUpdates(
        string fileHash, HashAlgorithm algo, CompSearchFilter? filter = null, CancellationToken ct = default)
    {
        var algoStr = algo switch
        {
            HashAlgorithm.Sha1 => "sha1",
            HashAlgorithm.Sha512 => "sha512",
            HashAlgorithm.Md5 => "md5",
            _ => "sha1"
        };

        var url = $"{BaseUrl}/version_file/{fileHash}/update";
        if (filter is not null)
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrEmpty(filter.GameVersion))
                queryParams.Add($"game_versions=[\"{filter.GameVersion}\"]");
            if (filter.Loaders.Count > 0)
                queryParams.Add($"loaders=[{string.Join(",", filter.Loaders.Select(l => $"\"{_LoaderToString(l)}\""))}]");
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);
        }

        await _rateLimiter.WaitIfNeeded(ct).ConfigureAwait(false);

        using var request = HttpRequest.Create(url).ApplyModrinthAuth(_accessToken);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var root = JsonSerializer.Deserialize<JsonElement>(
            await response.AsStringAsync(ct).ConfigureAwait(false), JsonCompat.SerializerOptions);
        return ModrinthModelConverter.ToFile(root);
    }

    public async Task<List<CompGameVersion>> GetGameVersions(CancellationToken ct = default)
    {
        var root = await _GetJsonAsync($"{BaseUrl}/tag/game_version", ct).ConfigureAwait(false);
        var results = new List<CompGameVersion>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                results.Add(ModrinthModelConverter.ToGameVersion(item));
        }
        return results;
    }

    public async Task<List<CompLoader>> GetLoaders(CancellationToken ct = default)
    {
        var root = await _GetJsonAsync($"{BaseUrl}/tag/loader", ct).ConfigureAwait(false);
        var results = new List<CompLoader>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                results.Add(ModrinthModelConverter.ToLoader(item));
        }
        return results;
    }

    public async Task<List<CompCategory>> GetCategories(CancellationToken ct = default)
    {
        var root = await _GetJsonAsync($"{BaseUrl}/tag/category", ct).ConfigureAwait(false);
        var results = new List<CompCategory>();
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                results.Add(ModrinthModelConverter.ToCategory(item));
        }
        return results;
    }

    private async Task<JsonElement> _GetJsonAsync(string url, CancellationToken ct)
    {
        await _rateLimiter.WaitIfNeeded(ct).ConfigureAwait(false);

        using var request = HttpRequest.Create(url).ApplyModrinthAuth(_accessToken);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);
        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var json = await response.AsStringAsync(ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<JsonElement>(json, JsonCompat.SerializerOptions);
    }

    private static async Task _EnsureSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.AsStringAsync(ct).ConfigureAwait(false);
        throw new CompApiException(
            response.StatusCode,
            "Modrinth",
            $"Modrinth API returned {(int)response.StatusCode}: {response.ReasonPhrase}",
            body);
    }

    private static string _BuildSearchUrl(CompSearchFilter filter)
    {
        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(filter.Query))
            queryParams.Add($"query={Uri.EscapeDataString(filter.Query)}");

        var facets = new List<string>();
        if (filter.ProjectType.HasValue && filter.ProjectType.Value != CompProjectType.Unknown)
        {
            var typeStr = filter.ProjectType.Value switch
            {
                CompProjectType.Mod => "mod",
                CompProjectType.Modpack => "modpack",
                CompProjectType.ResourcePack => "resourcepack",
                CompProjectType.Shader => "shader",
                CompProjectType.DataPack => "datapack",
                _ => null
            };
            if (typeStr is not null)
                facets.Add($"project_type:{typeStr}");
        }
        if (!string.IsNullOrEmpty(filter.Category))
            facets.Add($"categories:{filter.Category}");
        if (!string.IsNullOrEmpty(filter.GameVersion))
            facets.Add($"versions:{filter.GameVersion}");
        if (filter.Loaders.Count > 0)
        {
            foreach (var loader in filter.Loaders)
            {
                var loaderStr = _LoaderToString(loader);
                if (loaderStr is not null)
                    facets.Add($"categories:{loaderStr}");
            }
        }

        if (facets.Count > 0)
        {
            var facetsJson = JsonSerializer.Serialize(facets.Select(f => new[] { f }), JsonCompat.SerializerOptions);
            queryParams.Add($"facets={Uri.EscapeDataString(facetsJson)}");
        }

        var indexStr = filter.SortField switch
        {
            CompSortField.Downloads => "downloads",
            CompSortField.Follows => "follows",
            CompSortField.Updated => "updated",
            CompSortField.Created => "newest",
            _ => "relevance"
        };
        queryParams.Add($"index={indexStr}");

        queryParams.Add($"offset={filter.Offset}");
        queryParams.Add($"limit={filter.Limit}");

        return $"{BaseUrl}/search?{string.Join("&", queryParams)}";
    }

    private static string? _LoaderToString(ModLoaderType loader)
    {
        return loader switch
        {
            ModLoaderType.Forge => "forge",
            ModLoaderType.Fabric => "fabric",
            ModLoaderType.Quilt => "quilt",
            ModLoaderType.NeoForge => "neoforge",
            ModLoaderType.Rift => "rift",
            ModLoaderType.LiteLoader => "liteloader",
            ModLoaderType.Any => null,
            _ => null
        };
    }
}
