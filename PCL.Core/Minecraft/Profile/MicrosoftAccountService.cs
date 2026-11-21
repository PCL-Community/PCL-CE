using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile;

/// <summary>Microsoft 账号皮肤、披风与玩家名操作。</summary>
public static class MicrosoftAccountService
{
    private const string ServiceRoot = "https://api.minecraftservices.com";

    /// <summary>读取 Minecraft Services 档案，返回包含皮肤和披风信息的原始 JSON。</summary>
    public static async Task<JsonObject> GetProfileAsync(
        McProfile profile,
        CancellationToken token = default)
    {
        var json = await _SendJsonAsync(
                accessToken => HttpRequest
                    .Create($"{ServiceRoot}/minecraft/profile")
                    .WithBearerToken(accessToken),
                profile,
                token)
            .ConfigureAwait(false);
        return json;
    }

    /// <summary>读取完整披风列表。</summary>
    public static async Task<IReadOnlyList<CapeInfo>> GetCapesAsync(
        McProfile profile,
        CancellationToken token = default)
    {
        var json = await GetProfileAsync(profile, token).ConfigureAwait(false);
        var result = new List<CapeInfo>();
        if (json["capes"] is not JsonArray capes) return result;

        foreach (var node in capes)
        {
            if (node is not JsonObject cape) continue;
            var id = cape["id"]?.ToString();
            var alias = cape["alias"]?.ToString();
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(alias)) continue;
            result.Add(
                new CapeInfo(
                    id,
                    alias,
                    cape["url"]?.ToString(),
                    string.Equals(
                        cape["state"]?.ToString(),
                        "ACTIVE",
                        StringComparison.OrdinalIgnoreCase)));
        }

        return result;
    }

    /// <summary>设置当前披风；传入 <see langword="null" /> 表示移除披风。</summary>
    public static async Task SetCapeAsync(
        McProfile profile,
        string? capeId,
        CancellationToken token = default)
    {
        var response = await _SendAsync(
                accessToken => string.IsNullOrWhiteSpace(capeId)
                    ? HttpRequest
                        .CreateDelete($"{ServiceRoot}/minecraft/profile/capes/active")
                        .WithBearerToken(accessToken)
                    : HttpRequest
                        .CreatePut($"{ServiceRoot}/minecraft/profile/capes/active")
                        .WithBearerToken(accessToken)
                        .WithJsonContent(
                            new JsonObject
                            {
                                ["capeId"] = capeId
                            }),
                profile,
                token)
            .ConfigureAwait(false);
        using (response)
        {
            await _EnsureSuccessAsync(response, token).ConfigureAwait(false);
        }
    }

    /// <summary>上传皮肤并返回当前激活的皮肤地址。</summary>
    public static async Task<SkinUploadResult> UploadSkinAsync(
        McProfile profile,
        byte[] pngContent,
        string fileName,
        bool isSlim,
        CancellationToken token = default)
    {
        var response = await _SendAsync(
                accessToken =>
                {
                    var content = new MultipartFormDataContent
                    {
                        {
                            new StringContent(isSlim ? "slim" : "classic"), "variant"
                        },
                        {
                            new ByteArrayContent(pngContent), "file", fileName
                        }
                    };
                    return HttpRequest
                        .CreatePost($"{ServiceRoot}/minecraft/profile/skins")
                        .WithBearerToken(accessToken)
                        .WithContent(content);
                },
                profile,
                token,
                0)
            .ConfigureAwait(false);
        using (response)
        {
            await _EnsureSuccessAsync(response, token).ConfigureAwait(false);
            var json = await response
                .AsJsonAsync<JsonObject>(cancellationToken: token)
                .ConfigureAwait(false);
            var active = _FindActiveSkinUrl(json);
            if (active is null)
            {
                var latest = await GetProfileAsync(profile, token).ConfigureAwait(false);
                active = _FindActiveSkinUrl(latest);
            }

            return new SkinUploadResult(active);
        }
    }

    /// <summary>查询玩家名是否可用，返回服务端状态字符串。</summary>
    public static async Task<string?> CheckNameAvailableAsync(
        McProfile profile,
        string name,
        CancellationToken token = default)
    {
        var json = await _SendJsonAsync(
                accessToken => HttpRequest
                    .Create(
                        $"{ServiceRoot}/minecraft/profile/name/{Uri.EscapeDataString(name)}/available")
                    .WithBearerToken(accessToken),
                profile,
                token)
            .ConfigureAwait(false);
        return json["status"]?.ToString();
    }

    /// <summary>修改玩家名，返回服务端确认后的名称。</summary>
    public static async Task<string> RenameAsync(
        McProfile profile,
        string name,
        CancellationToken token = default)
    {
        var response = await _SendAsync(
                accessToken => HttpRequest
                    .CreatePut(
                        $"{ServiceRoot}/minecraft/profile/name/{Uri.EscapeDataString(name)}")
                    .WithBearerToken(accessToken)
                    .WithJsonContent(new JsonObject()),
                profile,
                token,
                0)
            .ConfigureAwait(false);
        using (response)
        {
            await _EnsureSuccessAsync(response, token).ConfigureAwait(false);
            var json = await response
                .AsJsonAsync<JsonObject>(cancellationToken: token)
                .ConfigureAwait(false);
            return json["name"]?.ToString() ?? name;
        }
    }

    private static string? _FindActiveSkinUrl(JsonObject? json)
    {
        if (json?["skins"] is not JsonArray skins) return null;
        foreach (var node in skins)
            if (node is JsonObject skin
                && string.Equals(
                    skin["state"]?.ToString(),
                    "ACTIVE",
                    StringComparison.OrdinalIgnoreCase))
                return skin["url"]?.ToString();

        return null;
    }

    private static async Task<JsonObject> _SendJsonAsync(
        Func<string, HttpRequestMessage> requestFactory,
        McProfile profile,
        CancellationToken token)
    {
        using var response = await _SendAsync(requestFactory, profile, token).ConfigureAwait(false);
        await _EnsureSuccessAsync(response, token).ConfigureAwait(false);
        return await response
                .AsJsonAsync<JsonObject>(cancellationToken: token)
                .ConfigureAwait(false)
            ?? new JsonObject();
    }

    private static async Task<HttpResponseMessage> _SendAsync(
        Func<string, HttpRequestMessage> requestFactory,
        McProfile profile,
        CancellationToken token,
        int retryTimes = 1)
    {
        var accessToken = await _GetAccessTokenAsync(profile, token).ConfigureAwait(false);
        var response = await requestFactory(accessToken)
            .SendAsync(
                NetworkService.GetClient(NetworkService.MinecraftService),
                retryTimes: retryTimes,
                cancellationToken: token)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        var refreshed = await _RefreshProfileAsync(profile, token).ConfigureAwait(false);
        return await requestFactory(refreshed.AccessToken)
            .SendAsync(
                NetworkService.GetClient(NetworkService.MinecraftService),
                retryTimes: retryTimes,
                cancellationToken: token)
            .ConfigureAwait(false);
    }

    private static async Task<string> _GetAccessTokenAsync(
        McProfile profile,
        CancellationToken token)
    {
        if (!profile.IsExpired && !string.IsNullOrWhiteSpace(profile.AccessToken))
            return profile.AccessToken;

        var refreshed = await _RefreshProfileAsync(profile, token).ConfigureAwait(false);
        return refreshed.AccessToken;
    }

    private static async Task<McProfile> _RefreshProfileAsync(
        McProfile profile,
        CancellationToken token)
    {
        if (profile.ProfileType != ProfileType.Microsoft)
            throw new InvalidOperationException("当前档案不是 Microsoft 账号。");

        return await ProfileService
            .AuthenticateAsync(
                ProfileType.Microsoft,
                new AuthenticationRequest
                {
                    RefreshToken = profile.RefreshToken,
                    ForceRefresh = true
                },
                profile,
                true,
                token)
            .ConfigureAwait(false);
    }

    private static async Task _EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        var content = await response.AsStringAsync(token).ConfigureAwait(false);
        throw new HttpRequestException(
            $"Minecraft Services 请求失败（{(int)response.StatusCode}）：{content}",
            null,
            response.StatusCode);
    }

    /// <summary>披风列表项。</summary>
    public sealed record CapeInfo(
        string Id,
        string Alias,
        string? Url,
        bool IsActive);

    /// <summary>皮肤上传结果。</summary>
    public sealed record SkinUploadResult(string? ActiveSkinUrl);
}