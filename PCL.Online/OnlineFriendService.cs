// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Serialization;

namespace PCL.Online;

public sealed record OnlineFriendProfile(string ProfileId, string Name);

public sealed record OnlineFriendRequest(
    string Id,
    string TargetProfileId,
    string TargetName,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record OnlineFriendPresence(string ProfileId, bool IsOnline, DateTimeOffset? LastSeen);

public static partial class OnlineFriendService
{
    public static async Task<OnlineFriendProfile?> SearchMinecraftProfileAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return null;

        try
        {
            var url = LooksLikeUuid(query)
                ? $"https://sessionserver.mojang.com/session/minecraft/profile/{NormalizeUuid(query)}"
                : $"https://api.mojang.com/users/profiles/minecraft/{Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await PortableHttp.Client
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NoContent ||
                response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            var json = PortableJson.ParseObject(await PortableHttp.ReadStringAsync(response, cancellationToken)
                .ConfigureAwait(false));
            var id = json["id"]?.ToString() ?? "";
            var name = json["name"]?.ToString() ?? query;
            return string.IsNullOrWhiteSpace(id) ? null : new OnlineFriendProfile(id, name);
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "搜索 Minecraft 档案失败");
            return null;
        }
    }

    public static async Task<bool> SendFriendRequestAsync(
        OnlineFriendProfile profile,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!OnlineAccountService.EnsureAccountIdentity())
                return false;

            var serverBaseUrl = CloudSyncService.ResolveServerBaseUrl();
            using var client = NCloudHttpClient.Create(serverBaseUrl);
            using var request = CreateJsonPostRequest(
                $"{serverBaseUrl}/api/friends/{Uri.EscapeDataString(OnlineAccountService.MsId)}/requests",
                new FriendRequestCreate(profile.ProfileId, profile.Name));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "发送好友申请失败");
            return false;
        }
    }

    public static async Task<IReadOnlyList<OnlineFriendRequest>> GetRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetRequestListAsync("requests", cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<OnlineFriendRequest>> GetHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return await GetRequestListAsync("history", cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IReadOnlyDictionary<string, OnlineFriendPresence>> GetPresenceAsync(
        IEnumerable<string> profileIds,
        CancellationToken cancellationToken = default)
    {
        var ids = profileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0)
            return new Dictionary<string, OnlineFriendPresence>(StringComparer.Ordinal);

        try
        {
            var serverBaseUrl = CloudSyncService.ResolveServerBaseUrl();
            using var client = NCloudHttpClient.Create(serverBaseUrl);
            using var request = CreateJsonPostRequest(
                $"{serverBaseUrl}/api/friends/presence",
                new PresenceRequest(ids));
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, OnlineFriendPresence>(StringComparer.Ordinal);

            var result = OnlineJson.Deserialize<PresenceResponse>(
                await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false));
            return (result?.Statuses ?? [])
                .ToDictionary(s => s.ProfileId, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "获取 PCL N 在线状态失败");
            return new Dictionary<string, OnlineFriendPresence>(StringComparer.Ordinal);
        }
    }

    private static async Task<IReadOnlyList<OnlineFriendRequest>> GetRequestListAsync(
        string segment,
        CancellationToken cancellationToken)
    {
        if (!OnlineAccountService.EnsureAccountIdentity())
            return [];

        try
        {
            var serverBaseUrl = CloudSyncService.ResolveServerBaseUrl();
            using var client = NCloudHttpClient.Create(serverBaseUrl);
            using var response = await client.GetAsync(
                    $"{serverBaseUrl}/api/friends/{Uri.EscapeDataString(OnlineAccountService.MsId)}/{segment}",
                    cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            return OnlineJson.Deserialize<List<OnlineFriendRequest>>(
                       await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false))
                   ?? [];
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", $"获取好友{segment}失败");
            return [];
        }
    }

    private static bool LooksLikeUuid(string value) => UuidRegex().IsMatch(value);

    private static string NormalizeUuid(string value) => value.Replace("-", "", StringComparison.Ordinal);

    private static HttpRequestMessage CreateJsonPostRequest<T>(string url, T value) => new(HttpMethod.Post, url)
    {
        Content = new StringContent(OnlineJson.Serialize(value), Encoding.UTF8, "application/json")
    };

    [GeneratedRegex("^[0-9a-fA-F]{32}$|^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidRegex();

}

internal sealed record FriendRequestCreate(string TargetProfileId, string TargetName);

internal sealed record PresenceRequest(IReadOnlyList<string> ProfileIds);

internal sealed record PresenceResponse(IReadOnlyList<OnlineFriendPresence> Statuses);
