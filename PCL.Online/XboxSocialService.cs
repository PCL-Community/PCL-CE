// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Serialization;

namespace PCL.Online;

public sealed record XboxFriendInfo(
    string Xuid,
    string Nickname,
    bool PclOnline,
    string PclOnlineText);

public static class XboxSocialService
{
    private static readonly (string Url, string ContractVersion)[] PeopleHubCandidates =
    [
        ("https://peoplehub.xboxlive.com/users/me/people/social/decoration/preferredColor,detail,multiplayerSummary,presenceDetail", "5"),
        ("https://peoplehub.xboxlive.com/users/me/people/social/decoration/preferredColor,detail,presenceDetail", "5"),
        ("https://peoplehub.xboxlive.com/users/me/people/social/decoration/detail,presenceDetail", "5"),
        ("https://peoplehub.xboxlive.com/users/me/people/social/decoration/detail", "5"),
        ("https://peoplehub.xboxlive.com/users/me/people/social/decoration/detail,presenceDetail", "3"),
        ("https://peoplehub.xboxlive.com/users/me/people/social/decoration/detail", "3")
    ];

    public static string? LastFailureReason { get; private set; }

    public static async Task<IReadOnlyList<XboxFriendInfo>> GetFriendsAsync(
        CancellationToken cancellationToken = default)
    {
        LastFailureReason = null;
        var authorization = OnlineAccountService.GetXboxAuthorization();
        if (authorization is null)
        {
            LastFailureReason = Text("Online.Friend.List.AuthorizationFailed");
            PortableLog.Warn("Online", "无法获取 Xbox 授权，好友列表为空。");
            return [];
        }

        try
        {
            var json = await FetchPeopleHubAsync(authorization, cancellationToken).ConfigureAwait(false);
            if (json is null)
            {
                LastFailureReason = Text("Online.Friend.List.LoadFailed");
                return [];
            }

            var people = json["people"] as JsonArray ?? [];
            var entries = people
                .OfType<JsonObject>()
                .Select(ToFriendInfo)
                .Where(friend => !string.IsNullOrWhiteSpace(friend.Xuid))
                .ToList();

            var statuses = await OnlineFriendService.GetPresenceAsync(entries.Select(e => e.Xuid), cancellationToken)
                .ConfigureAwait(false);
            return entries.Select(entry =>
            {
                var isOnline = statuses.TryGetValue(entry.Xuid, out var status) && status.IsOnline;
                return entry with
                {
                    PclOnline = isOnline,
                    PclOnlineText = isOnline
                        ? Text("Online.Friend.Status.Online")
                        : Text("Online.Friend.Status.Offline")
                };
            }).ToList();
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "获取 Xbox 好友失败");
            LastFailureReason = Text("Online.Friend.List.LoadFailed");
            return [];
        }
    }

    private static async Task<JsonObject?> FetchPeopleHubAsync(
        XboxAuthorization authorization,
        CancellationToken cancellationToken)
    {
        foreach (var (url, contractVersion) in PeopleHubCandidates)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Authorization",
                    $"XBL3.0 x={authorization.UserHash};{authorization.XstsToken}");
                request.Headers.TryAddWithoutValidation("x-xbl-contract-version", contractVersion);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("Accept-Language", BuildAcceptLanguageHeader());
                request.Headers.TryAddWithoutValidation("User-Agent", "PCLN/2.15.0");

                using var response = await PortableHttp.Client
                    .SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                var body = await PortableHttp.ReadStringAsync(response, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    PortableLog.Debug("Online",
                        $"Xbox 好友接口暂不可用（HTTP {(int)response.StatusCode}, Contract {contractVersion}）：{TrimForLog(body)}");
                    continue;
                }

                var json = PortableJson.ParseObject(body);
                var peopleCount = (json["people"] as JsonArray)?.Count ?? 0;
                PortableLog.Debug("Online", $"Xbox 好友接口返回 {peopleCount} 个联系人（Contract {contractVersion}）。");
                return json;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                PortableLog.Debug(ex, "Online", $"获取 Xbox 好友接口失败（Contract {contractVersion}）");
            }
        }

        return null;
    }

    private static XboxFriendInfo ToFriendInfo(JsonObject person)
    {
        var xuid = person["xuid"]?.ToString() ?? "";
        var nickname = FirstNonEmpty(
            person["displayName"]?.ToString(),
            person["gamertag"]?.ToString(),
            person["realName"]?.ToString(),
            xuid);
        return new XboxFriendInfo(xuid, nickname, false, Text("Online.Friend.Status.Offline"));
    }

    private static string Text(string key, params object?[] args)
    {
        return OnlineRuntime.Host.Text(key, args);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;

        return "";
    }

    private static string BuildAcceptLanguageHeader()
    {
        var culture = CultureInfo.CurrentUICulture;
        var name = string.IsNullOrWhiteSpace(culture.Name) ? "en-US" : culture.Name;
        if (name.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            return "en-US,en;q=0.8";

        var parentName = culture.Parent?.Name;
        if (!string.IsNullOrWhiteSpace(parentName) &&
            !parentName.Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return $"{name},{parentName};q=0.9,en-US;q=0.8,en;q=0.7";
        }

        return $"{name},en-US;q=0.8,en;q=0.7";
    }

    private static string TrimForLog(string value)
    {
        value = value.ReplaceLineEndings(" ").Trim();
        return value.Length <= 300 ? value : value[..300] + "...";
    }
}
