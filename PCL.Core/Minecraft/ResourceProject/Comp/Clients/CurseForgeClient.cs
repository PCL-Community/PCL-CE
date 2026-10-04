using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

public sealed class CurseForgeClient : ICompClient
{
    private const string BaseUrl = "https://api.curseforge.com";
    private const int GameId = 432;

    private readonly string _apiKey;
    private readonly HttpClient? _httpClient;
    private readonly RateLimiter _rateLimiter;

    public CurseForgeClient(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _httpClient = httpClient;
        _rateLimiter = new RateLimiter(300);
    }

    public async Task<CompSearchResult> SearchProjects(CompSearchFilter filter, CancellationToken ct = default)
    {
        var url = _BuildSearchUrl(filter);
        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        return CurseForgeModelConverter.ToSearchResult(root, filter.Offset, filter.Limit);
    }

    public async Task<CompProject> GetProject(string projectId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/v1/mods/{projectId}";
        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        var data = root.GetProperty("data");
        return CurseForgeModelConverter.ToProject(data);
    }

    public async Task<List<CompProject>> GetProjects(IEnumerable<string> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.ToList();
        if (ids.Count == 0) return [];

        var url = $"{BaseUrl}/v1/mods";
        var body = JsonSerializer.Serialize(new { modIds = ids.Select(int.Parse).ToList() }, JsonCompat.SerializerOptions);

        using var request = HttpRequest.CreatePost(url)
            .WithContent(body, "application/json")
            .ApplyCurseForgeAuth(_apiKey);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);
        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var root = JsonSerializer.Deserialize<JsonElement>(await response.AsStringAsync(ct).ConfigureAwait(false), JsonCompat.SerializerOptions);
        var data = root.GetProperty("data");
        var results = new List<CompProject>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToProject(item));
        }
        return results;
    }

    public async Task<List<CompProject>> GetFeaturedProjects(string? gameVersion = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/v1/mods/featured";
        var bodyObj = new Dictionary<string, object> { ["gameId"] = GameId };
        if (!string.IsNullOrEmpty(gameVersion))
            bodyObj["featuredFilter"] = gameVersion;

        var body = JsonSerializer.Serialize(bodyObj, JsonCompat.SerializerOptions);

        using var request = HttpRequest.CreatePost(url)
            .WithContent(body, "application/json")
            .ApplyCurseForgeAuth(_apiKey);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);
        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var root = JsonSerializer.Deserialize<JsonElement>(await response.AsStringAsync(ct).ConfigureAwait(false), JsonCompat.SerializerOptions);
        var data = root.GetProperty("data");

        var results = new List<CompProject>();
        if (data.TryGetProperty("featured", out var featured) && featured.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in featured.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToProject(item));
        }
        if (data.TryGetProperty("popular", out var popular) && popular.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in popular.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToProject(item));
        }
        if (data.TryGetProperty("recentlyUpdated", out var updated) && updated.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in updated.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToProject(item));
        }
        return results;
    }

    public async Task<string> GetProjectDescription(string projectId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/v1/mods/{projectId}/description";
        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        return root.GetProperty("data").GetString() ?? "";
    }

    public async Task<List<CompFile>> GetProjectFiles(string projectId, CompSearchFilter? filter = null, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/v1/mods/{projectId}/files";
        if (filter is not null)
        {
            var queryParams = new List<string>();
            if (filter.Offset > 0) queryParams.Add($"index={filter.Offset}");
            if (filter.Limit > 0) queryParams.Add($"pageSize={filter.Limit}");
            if (!string.IsNullOrEmpty(filter.GameVersion))
                queryParams.Add($"gameVersion={Uri.EscapeDataString(filter.GameVersion)}");
            if (filter.Loaders.Count > 0)
                queryParams.Add($"modLoaderType={(int)filter.Loaders[0]}");
            if (queryParams.Count > 0)
                url += "?" + string.Join("&", queryParams);
        }

        var root = await _GetJsonAsync(url, ct).ConfigureAwait(false);
        var data = root.GetProperty("data");
        var results = new List<CompFile>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToFile(item, int.Parse(projectId)));
        }
        return results;
    }

    public async Task<CompFile> GetFile(string fileId, CancellationToken ct = default)
    {
        // CurseForge files API requires modId, so use the bulk endpoint for a single file
        var body = JsonSerializer.Serialize(new { fileIds = new[] { int.Parse(fileId) } }, JsonCompat.SerializerOptions);

        using var request = HttpRequest.CreatePost($"{BaseUrl}/v1/mods/files")
            .WithContent(body, "application/json")
            .ApplyCurseForgeAuth(_apiKey);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);
        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var root = JsonSerializer.Deserialize<JsonElement>(
            await response.AsStringAsync(ct).ConfigureAwait(false), JsonCompat.SerializerOptions);
        var data = root.GetProperty("data");
        if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
        {
            var fileEl = data[0];
            var modId = fileEl.TryGetProperty("modId", out var mid) ? mid.GetInt32() : 0;
            return CurseForgeModelConverter.ToFile(fileEl, modId);
        }
        throw new InvalidOperationException($"File {fileId} not found on CurseForge.");
    }

    public async Task<string> GetFileDownloadUrl(string fileId, CancellationToken ct = default)
    {
        var file = await GetFile(fileId, ct).ConfigureAwait(false);
        return file.DownloadUrl?.ToString() ?? "";
    }

    public async Task<string> GetFileChangelog(string fileId, CancellationToken ct = default)
    {
        // Need modId for changelog endpoint; fetch file first to get it
        var body = JsonSerializer.Serialize(new { fileIds = new[] { int.Parse(fileId) } }, JsonCompat.SerializerOptions);

        using var request = HttpRequest.CreatePost($"{BaseUrl}/v1/mods/files")
            .WithContent(body, "application/json")
            .ApplyCurseForgeAuth(_apiKey);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);
        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var root = JsonSerializer.Deserialize<JsonElement>(
            await response.AsStringAsync(ct).ConfigureAwait(false), JsonCompat.SerializerOptions);
        var data = root.GetProperty("data");
        if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
        {
            var fileEl = data[0];
            var modId = fileEl.TryGetProperty("modId", out var mid) ? mid.GetInt32() : 0;

            var url = $"{BaseUrl}/v1/mods/{modId}/files/{fileId}/changelog";
            var changelogRoot = await _GetJsonAsync(url, ct).ConfigureAwait(false);
            return changelogRoot.GetProperty("data").GetString() ?? "";
        }
        return "";
    }

    public async Task<Dictionary<string, List<CompFile>>> MatchFingerprints(
        IEnumerable<string> hashes, HashAlgorithm algo, CancellationToken ct = default)
    {
        var hashList = hashes.ToList();
        if (hashList.Count == 0) return new Dictionary<string, List<CompFile>>();

        var numericHashes = hashList.Select(h => _ToNumericHash(h)).ToList();

        var body = JsonSerializer.Serialize(
            new { fingerprints = numericHashes },
            JsonCompat.SerializerOptions);

        using var request = HttpRequest.CreatePost($"{BaseUrl}/v1/fingerprints")
            .WithContent(body, "application/json")
            .ApplyCurseForgeAuth(_apiKey);
        using var response = await request.SendAsync(httpClient: _httpClient, addMetedata: _httpClient is null, cancellationToken: ct).ConfigureAwait(false);
        await _EnsureSuccess(response, ct).ConfigureAwait(false);

        var root = JsonSerializer.Deserialize<JsonElement>(
            await response.AsStringAsync(ct).ConfigureAwait(false), JsonCompat.SerializerOptions);

        var result = new Dictionary<string, List<CompFile>>();
        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("exactMatches", out var matches) &&
            matches.ValueKind == JsonValueKind.Array)
        {
            foreach (var match in matches.EnumerateArray())
            {
                if (match.TryGetProperty("id", out var idProp))
                {
                    var fileId = idProp.GetInt32().ToString();
                    if (match.TryGetProperty("file", out var fileEl))
                    {
                        var modId = fileEl.TryGetProperty("modId", out var mi) ? mi.GetInt32() : 0;
                        var compFile = CurseForgeModelConverter.ToFile(fileEl, modId);
                        result[fileId] = [compFile];
                    }
                }
            }
        }
        return result;
    }

    public async Task<CompFile?> CheckForUpdates(
        string fileHash, HashAlgorithm algo, CompSearchFilter? filter = null, CancellationToken ct = default)
    {
        var matchResult = await MatchFingerprints([fileHash], algo, ct).ConfigureAwait(false);
        if (matchResult.Count == 0) return null;

        var (fileId, files) = matchResult.First();
        var firstFile = files.FirstOrDefault();
        if (firstFile is null) return null;

        var projectFiles = await GetProjectFiles(firstFile.ProjectId, filter, ct).ConfigureAwait(false);
        return projectFiles.Count > 0 ? projectFiles[0] : null;
    }

    public async Task<List<CompGameVersion>> GetGameVersions(CancellationToken ct = default)
    {
        var root = await _GetJsonAsync($"{BaseUrl}/v1/minecraft/version", ct).ConfigureAwait(false);
        var data = root.GetProperty("data");
        var results = new List<CompGameVersion>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToGameVersion(item));
        }
        return results;
    }

    public async Task<List<CompLoader>> GetLoaders(CancellationToken ct = default)
    {
        var root = await _GetJsonAsync($"{BaseUrl}/v1/minecraft/modloader", ct).ConfigureAwait(false);
        var data = root.GetProperty("data");
        var results = new List<CompLoader>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToLoader(item));
        }
        return results;
    }

    public async Task<List<CompCategory>> GetCategories(CancellationToken ct = default)
    {
        var root = await _GetJsonAsync($"{BaseUrl}/v1/categories?gameId={GameId}", ct).ConfigureAwait(false);
        var data = root.GetProperty("data");
        var results = new List<CompCategory>();
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
                results.Add(CurseForgeModelConverter.ToCategory(item));
        }
        return results;
    }

    private async Task<JsonElement> _GetJsonAsync(string url, CancellationToken ct)
    {
        await _rateLimiter.WaitIfNeeded(ct).ConfigureAwait(false);

        using var request = HttpRequest.Create(url)
            .ApplyCurseForgeAuth(_apiKey);
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
            "CurseForge",
            $"CurseForge API returned {(int)response.StatusCode}: {response.ReasonPhrase}",
            body);
    }

    private static string _BuildSearchUrl(CompSearchFilter filter)
    {
        var queryParams = new List<string>
        {
            $"gameId={GameId}"
        };

        if (!string.IsNullOrEmpty(filter.Query))
            queryParams.Add($"searchFilter={Uri.EscapeDataString(filter.Query)}");
        if (!string.IsNullOrEmpty(filter.Category))
            queryParams.Add($"categoryId={filter.Category}");
        if (!string.IsNullOrEmpty(filter.GameVersion))
            queryParams.Add($"gameVersion={Uri.EscapeDataString(filter.GameVersion)}");
        if (filter.Loaders.Count > 0)
            queryParams.Add($"modLoaderType={(int)filter.Loaders[0]}");
        if (filter.SortField != CompSortField.Relevance)
        {
            var fieldMap = new Dictionary<CompSortField, int>
            {
                [CompSortField.Downloads] = 2,
                [CompSortField.Updated] = 3,
                [CompSortField.Created] = 1,
            };
            if (fieldMap.TryGetValue(filter.SortField, out var fieldId))
                queryParams.Add($"sortField={fieldId}");
        }
        queryParams.Add($"sortOrder={(filter.SortOrder == SortOrder.Asc ? "asc" : "desc")}");
        queryParams.Add($"index={filter.Offset}");
        queryParams.Add($"pageSize={filter.Limit}");

        return $"{BaseUrl}/v1/mods/search?{string.Join("&", queryParams)}";
    }

    private static long _ToNumericHash(string hash)
    {
        if (long.TryParse(hash, System.Globalization.NumberStyles.HexNumber, null, out var result))
            return result;
        return hash.GetHashCode();
    }
}
