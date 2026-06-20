// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Serialization;

namespace PCL.Online;

public sealed record ModrinthServerEntry(
    string ProjectId,
    string Slug,
    string Title,
    string Description,
    string Address,
    string IconUrl,
    IReadOnlyList<string> Versions,
    int PlayersOnline,
    int PlayersMax);

public enum ModrinthServerSort
{
    Relevance,
    Downloads,
    Follows,
    Newest,
    Updated
}

public sealed record ModrinthServerSearchOptions(
    string SearchText = "",
    string GameVersion = "",
    ModrinthServerSort Sort = ModrinthServerSort.Relevance,
    int Offset = 0,
    int Limit = ModrinthServerCatalog.DefaultPageSize);

public sealed record ModrinthServerSearchResult(
    IReadOnlyList<ModrinthServerEntry> Entries,
    int TotalHits,
    int Offset,
    int Limit);

public static class ModrinthServerCatalog
{
    public const int DefaultPageSize = 40;
    private const int MaxPageSize = 100;
    private const int DetailConcurrency = 8;
    private const string UserAgent = "PCLN/2.15.0 (github.com/MuXue1230-owo/PCL-N)";

    public static async Task<IReadOnlyList<ModrinthServerEntry>> GetOnlineServersAsync(
        int limit = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await SearchOnlineServersAsync(new ModrinthServerSearchOptions(Limit: limit), cancellationToken)
            .ConfigureAwait(false);
        return result.Entries;
    }

    public static async Task<ModrinthServerSearchResult> SearchOnlineServersAsync(
        ModrinthServerSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var limit = Math.Clamp(options.Limit, 1, MaxPageSize);
            var offset = Math.Max(0, options.Offset);
            var facets = BuildFacets(options.GameVersion);
            var url = $"https://api.modrinth.com/v2/search?facets={facets}" +
                      $"&index={GetSortIndex(options.Sort)}" +
                      $"&limit={limit}" +
                      $"&offset={offset}";
            if (!string.IsNullOrWhiteSpace(options.SearchText))
                url += $"&query={Uri.EscapeDataString(options.SearchText.Trim())}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var response = await PortableHttp.Client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = PortableJson.ParseObject(await PortableHttp.ReadStringAsync(response, cancellationToken)
                .ConfigureAwait(false));
            var hits = (json["hits"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .ToList();
            var totalHits = ReadInt(json["total_hits"]);

            using var gate = new SemaphoreSlim(DetailConcurrency);
            var detailTasks = hits.Select(hit => GetDetailsWithGateAsync(hit, gate, cancellationToken)).ToArray();
            var details = await Task.WhenAll(detailTasks).ConfigureAwait(false);
            var entries = details
                .Where(entry => entry is not null && !string.IsNullOrWhiteSpace(entry.Address))
                .Select(entry => entry!)
                .ToList();
            return new ModrinthServerSearchResult(entries, totalHits, offset, limit);
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "获取 Modrinth 服务器列表失败");
            return new ModrinthServerSearchResult([], 0, Math.Max(0, options.Offset), Math.Clamp(options.Limit, 1, MaxPageSize));
        }
    }

    private static string BuildFacets(string gameVersion)
    {
        string[][] facets = string.IsNullOrWhiteSpace(gameVersion)
            ? [["project_type:minecraft_java_server"]]
            :
            [
                ["project_type:minecraft_java_server"],
                [$"versions:'{gameVersion.Trim()}'"]
            ];
        return Uri.EscapeDataString(OnlineJson.Serialize(facets));
    }

    private static string GetSortIndex(ModrinthServerSort sort) => sort switch
    {
        ModrinthServerSort.Downloads => "downloads",
        ModrinthServerSort.Follows => "follows",
        ModrinthServerSort.Newest => "newest",
        ModrinthServerSort.Updated => "updated",
        _ => "relevance"
    };

    private static async Task<ModrinthServerEntry?> GetDetailsWithGateAsync(
        JsonObject hit,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await GetDetailsAsync(hit, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<ModrinthServerEntry?> GetDetailsAsync(
        JsonObject hit,
        CancellationToken cancellationToken)
    {
        var projectId = hit["project_id"]?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(projectId))
            return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.modrinth.com/v3/project/{Uri.EscapeDataString(projectId)}");
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var response = await PortableHttp.Client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var detail = PortableJson.ParseObject(await PortableHttp.ReadStringAsync(response, cancellationToken)
                .ConfigureAwait(false));
            return ParseServerEntry(hit, detail);
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", $"获取 Modrinth 服务器详情失败：{projectId}");
            return null;
        }
    }

    internal static ModrinthServerEntry? ParseServerEntry(JsonObject hit, JsonObject detail)
    {
        var projectId = detail["id"]?.ToString() ?? hit["project_id"]?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(projectId))
            return null;

        var server = detail["minecraft_java_server"] as JsonObject;
        var ping = server?["ping"]?["data"] as JsonObject;
        var versions = (detail["game_versions"] as JsonArray ??
                        hit["versions"] as JsonArray ??
                        [])
            .Select(v => v?.ToString() ?? "")
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new ModrinthServerEntry(
            projectId,
            detail["slug"]?.ToString() ?? hit["slug"]?.ToString() ?? "",
            detail["name"]?.ToString() ?? hit["title"]?.ToString() ?? projectId,
            detail["summary"]?.ToString() ?? hit["description"]?.ToString() ?? "",
            server?["address"]?.ToString() ?? "",
            detail["icon_url"]?.ToString() ?? hit["icon_url"]?.ToString() ?? "",
            versions,
            ReadInt(ping?["players_online"]),
            ReadInt(ping?["players_max"]));
    }

    private static int ReadInt(JsonNode? node) =>
        int.TryParse(node?.ToString(), out var value) ? value : 0;
}
