// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.Core.IO.Net;
using PCL.Core.Logging;
using PCL.Core.Serialization;

namespace PCL.Online;

public class OnlineLoginResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? MsId { get; init; }
    public string? UserName { get; init; }
    public string? DisplayName { get; init; }
    public string? MinecraftProfileName { get; init; }
    public string? Uuid { get; init; }
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public bool OwnsMinecraft { get; init; }
    public bool HasMinecraftProfile { get; init; }
    public bool MinecraftProfileMissing { get; init; }
}

public sealed record XboxAuthorization(string XstsToken, string UserHash);

public static class OnlineAccountService
{
    private const string GraphScope = "https://graph.microsoft.com/User.Read openid profile offline_access";
    private const string XboxScope = "XboxLive.signin offline_access";
    private static Func<string, XboxAuthorization?>? _platformXboxAuthorizationProvider;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(UserName);
    public static string MsId => GetOnlineString("MsId");
    public static string? UserName => GetOnlineString("MsUserName");
    public static string? AvatarUrl => GetOnlineString("MsAvatarUrl");
    public static bool OwnsMinecraft => GetOnlineBoolean("MsOwnsMinecraft");

    private static string ClientId => OnlineRuntime.Host.GetSecret("MS_CLIENT_ID") ?? "";
    public static string MicrosoftClientId => ClientId;

    private static string Text(string key, params object?[] args) => OnlineRuntime.Host.Text(key, args);

    private static string GetOnlineString(string key) => OnlineRuntime.Host.GetString($"Online.{key}");

    private static void SetOnlineString(string key, string value) => OnlineRuntime.Host.SetString($"Online.{key}", value);

    private static bool GetOnlineBoolean(string key) => OnlineRuntime.Host.GetBoolean($"Online.{key}");

    private static void SetOnlineBoolean(string key, bool value) => OnlineRuntime.Host.SetBoolean($"Online.{key}", value);

    private static void FlushState() => OnlineRuntime.Host.Flush();

    public static XboxAuthorization? GetXboxAuthorization(string relyingParty = "http://xboxlive.com")
    {
        var clientId = ClientId;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            foreach (var refreshToken in EnumerateDistinctTokens(
                         GetOnlineString("MsOAuthRefreshToken"),
                         GetOnlineString("MsGraphRefreshToken")))
            {
                var tokens = ExchangeRefreshToken(clientId, refreshToken, XboxScope);
                if (tokens.AccessToken is null)
                {
                    PortableLog.Warn("Online", $"无法刷新 Xbox 令牌：{tokens.Error}");
                    continue;
                }

                SetOnlineString("MsOAuthRefreshToken", tokens.RefreshToken ?? refreshToken);
                SetOnlineString("MsLastTokenRefresh", DateTime.Now.ToString("O"));
                FlushState();

                var authorization = CreateXboxAuthorization(tokens.AccessToken, relyingParty);
                if (authorization is not null)
                    return authorization;
            }
        }

        return _platformXboxAuthorizationProvider?.Invoke(relyingParty);
    }

    public static void RegisterPlatformXboxAuthorizationProvider(Func<string, XboxAuthorization?>? provider)
    {
        _platformXboxAuthorizationProvider = provider;
    }

    public static bool EnsureAccountIdentity()
    {
        if (!string.IsNullOrWhiteSpace(MsId))
            return true;

        var clientId = ClientId;
        var refreshToken = GetOnlineString("MsGraphRefreshToken");
        if (string.IsNullOrWhiteSpace(refreshToken))
            refreshToken = GetOnlineString("MsOAuthRefreshToken");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var graphTokens = ExchangeRefreshToken(clientId, refreshToken, GraphScope);
        if (graphTokens.AccessToken is null)
        {
            PortableLog.Warn("Online", $"无法补全 Microsoft 账户 ID：{graphTokens.Error}");
            return false;
        }

        var graphProfile = FetchGraphProfile(graphTokens.AccessToken);
        if (string.IsNullOrWhiteSpace(graphProfile.id))
            return false;

        SetOnlineString("MsId", graphProfile.id);
        SetOnlineString("MsGraphAccessToken", graphTokens.AccessToken);
        SetOnlineString("MsGraphRefreshToken", graphTokens.RefreshToken ?? refreshToken);
        if (string.IsNullOrWhiteSpace(GetOnlineString("MsOAuthRefreshToken")))
            SetOnlineString("MsOAuthRefreshToken", refreshToken);
        if (!string.IsNullOrWhiteSpace(graphProfile.name))
            SetOnlineString("MsUserName", graphProfile.name);
        if (!string.IsNullOrWhiteSpace(graphProfile.avatarUrl))
            SetOnlineString("MsAvatarUrl", graphProfile.avatarUrl);
        SetOnlineString("MsLastTokenRefresh", DateTime.Now.ToString("O"));
        FlushState();
        return true;
    }

    public static OnlineLoginResult Login(Func<JsonObject, object?> showLoginDialog)
    {
        return LoginCore(showLoginDialog, Text("Online.Login.Title"));
    }

    public static OnlineLoginResult CompleteLoginWithAccessTokens(string? graphAccessToken, string xboxAccessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xboxAccessToken);
        return CompleteLogin(new OAuthTokens(graphAccessToken), new OAuthTokens(xboxAccessToken));
    }

    public static XboxAuthorization? CreateXboxAuthorizationFromMicrosoftAccessToken(
        string accessToken,
        string relyingParty)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(relyingParty);
        return CreateXboxAuthorization(accessToken, relyingParty);
    }

    private static OnlineLoginResult LoginCore(Func<JsonObject, object?> showLoginDialog, string title)
    {
        try
        {
            var clientId = ClientId;
            if (string.IsNullOrEmpty(clientId))
                return new OnlineLoginResult
                    { Success = false, Message = Text("Online.Login.ClientIdMissing") };

            var xboxTokens = Authorize(clientId, XboxScope, title, showLoginDialog);
            if (xboxTokens.Error is not null)
                return new OnlineLoginResult { Success = false, Message = xboxTokens.Error };

            // 一个 access token 只能用于一个资源。首次授权后使用刷新令牌，
            // 静默换取应用已获得同意的 Microsoft Graph token。
            var graphTokens = ExchangeRefreshToken(clientId, xboxTokens.RefreshToken!, GraphScope);
            if (graphTokens.Error is not null)
                PortableLog.Warn("Online", $"无法静默获取 Microsoft Graph 令牌：{graphTokens.Error}");

            return CompleteLogin(graphTokens, xboxTokens);
        }
        catch (Exception ex) when (ex.Message == "$$")
        {
            return new OnlineLoginResult { Success = false, Message = Text("Online.Login.Cancelled") };
        }
        catch (Exception ex)
        {
            PortableLog.Debug(ex, "Online", "登录失败");
            return new OnlineLoginResult { Success = false, Message = ex.Message };
        }
    }

    private static OAuthTokens Authorize(string clientId, string scope, string title,
        Func<JsonObject, object?> showLoginDialog)
    {
        var body = $"client_id={Uri.EscapeDataString(clientId)}&scope={Uri.EscapeDataString(scope)}";
        JsonObject prepareJson;
        using (var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded"))
        using (var response = PortableHttp.Client
                   .PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode", content)
                   .GetAwaiter().GetResult())
        {
            response.EnsureSuccessStatusCode();
            prepareJson = PortableJson.ParseObject(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            prepareJson["scope"] = scope;
            prepareJson["login_title"] = title;
        }

        var result = showLoginDialog(prepareJson);
        if (result is Exception ex)
            return new OAuthTokens(Error: ex.Message);
        if (result is not string[] oauthResult || oauthResult.Length < 2)
            return new OAuthTokens(Error: Text("Online.Login.Cancelled"));

        return new OAuthTokens(oauthResult[0], oauthResult[1]);
    }

    private static OAuthTokens ExchangeRefreshToken(string clientId, string refreshToken, string scope)
    {
        try
        {
            var body = $"client_id={Uri.EscapeDataString(clientId)}" +
                       "&grant_type=refresh_token" +
                       $"&refresh_token={Uri.EscapeDataString(refreshToken)}" +
                       $"&scope={Uri.EscapeDataString(scope)}";
            using var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
            using var response = PortableHttp.Client
                .PostAsync("https://login.microsoftonline.com/consumers/oauth2/v2.0/token", content)
                .GetAwaiter().GetResult();
            var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                var errorJson = PortableJson.ParseNode(responseBody) as JsonObject;
                var error = errorJson?["error"]?.ToString() ?? $"HTTP {(int)response.StatusCode}";
                var description = errorJson?["error_description"]?.ToString();
                return new OAuthTokens(Error: string.IsNullOrEmpty(description)
                    ? error
                    : $"{error}: {description}");
            }

            var result = PortableJson.ParseObject(responseBody);
            return new OAuthTokens(
                result["access_token"]?.ToString(),
                result["refresh_token"]?.ToString() ?? refreshToken);
        }
        catch (Exception ex)
        {
            return new OAuthTokens(Error: ex.Message);
        }
    }

    private static OnlineLoginResult CompleteLogin(OAuthTokens graphTokens, OAuthTokens xboxTokens)
    {
        var graphProfile = graphTokens.AccessToken is null
            ? (id: (string?)null, name: (string?)null, avatarUrl: (string?)null)
            : FetchGraphProfile(graphTokens.AccessToken);

        var xblToken = AuthXbl(xboxTokens.AccessToken!);
        if (xblToken is null) return Fail(Text("Online.Login.XboxFailed"));

        var xsts = AuthXsts(xblToken, "rp://api.minecraftservices.com/");
        if (xsts is null) return Fail(Text("Online.Login.XstsFailed"));
        var xstsToken = xsts["Token"]?.ToString();
        var userHash = xsts["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString();
        if (string.IsNullOrEmpty(xstsToken) || string.IsNullOrEmpty(userHash))
            return Fail(Text("Online.Login.XstsCredentialMissing"));

        var mcToken = AuthMc(xstsToken, userHash);
        if (mcToken is null) return Fail(Text("Online.Login.MinecraftAuthFailed"));

        var mcProfileResult = GetProfile(mcToken);
        if (mcProfileResult.ErrorMessage is not null)
            return Fail(mcProfileResult.ErrorMessage);

        var mcProfile = mcProfileResult.Profile;
        var hasMcProfile = mcProfile is not null;
        var mcName = hasMcProfile
            ? mcProfile?["name"]?.ToString() ?? Text("Common.State.Unknown")
            : "";
        var uuid = hasMcProfile ? mcProfile?["id"]?.ToString() ?? "" : "";

        var displayName = FirstNonEmpty(graphProfile.name, mcName, graphProfile.id,
            Text("Online.Login.MicrosoftAccount"));
        var xboxRefreshToken = FirstNonEmpty(
            xboxTokens.RefreshToken,
            GetOnlineString("MsOAuthRefreshToken"),
            GetOnlineString("MsGraphRefreshToken"));
        var graphRefreshToken = FirstNonEmpty(
            graphTokens.RefreshToken,
            GetOnlineString("MsGraphRefreshToken"),
            GetOnlineString("MsOAuthRefreshToken"));

        var ownsMc = CheckOwnership(mcToken);

        SetOnlineString("MsAccessToken", mcToken);
        SetOnlineString("MsOAuthRefreshToken", xboxRefreshToken);
        SetOnlineString("MsGraphAccessToken", graphTokens.AccessToken ?? "");
        SetOnlineString("MsGraphRefreshToken", graphRefreshToken);
        SetOnlineString("MsId", graphProfile.id ?? "");
        SetOnlineString("MsUserName", displayName);
        SetOnlineString("MsMinecraftProfileName", mcName);
        SetOnlineString("MsUuid", uuid);
        SetOnlineString("MsAvatarUrl", graphProfile.avatarUrl ?? "");
        SetOnlineBoolean("MsOwnsMinecraft", ownsMc);
        SetOnlineString("MsLastTokenRefresh", DateTime.Now.ToString("O"));
        FlushState();

        var messageKey = (hasMcProfile, ownsMc) switch
        {
            (true, true) => "Online.Login.SuccessOwned",
            (false, true) => "Online.Login.SuccessProfileMissing",
            _ => "Online.Login.SuccessNotOwned"
        };

        return new OnlineLoginResult
        {
            Success = true,
            Message = Text(messageKey, displayName),
            MsId = graphProfile.id,
            UserName = hasMcProfile ? mcName : displayName,
            DisplayName = displayName,
            MinecraftProfileName = mcName,
            Uuid = uuid,
            AccessToken = mcToken,
            RefreshToken = xboxRefreshToken,
            OwnsMinecraft = ownsMc,
            HasMinecraftProfile = hasMcProfile,
            MinecraftProfileMissing = mcProfileResult.NotFound
        };
    }

    private static (string? id, string? name, string? avatarUrl) FetchGraphProfile(string msToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            req.Headers.Add("Authorization", $"Bearer {msToken}");
            using var r = PortableHttp.Client.SendAsync(req).GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            var json = PortableJson.ParseObject(r.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            var name = json["displayName"]?.ToString();
            var userId = json["id"]?.ToString();

            string? avatar = null;
            try
            {
                using var photoReq = new HttpRequestMessage(HttpMethod.Get,
                    "https://graph.microsoft.com/v1.0/me/photo/$value");
                photoReq.Headers.Add("Authorization", $"Bearer {msToken}");
                using var photoResp = PortableHttp.Client
                    .SendAsync(photoReq).GetAwaiter().GetResult();
                if (photoResp.IsSuccessStatusCode)
                {
                    var bytes = photoResp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var dir = Path.Combine(OnlineRuntime.Host.SharedDataDirectory, "Avatars");
                    Directory.CreateDirectory(dir);
                    var fileName = string.IsNullOrEmpty(userId) ? Guid.NewGuid().ToString("N") : userId;
                    var path = Path.Combine(dir, $"{fileName}.jpg");
                    File.WriteAllBytes(path, bytes);
                    avatar = path;
                }
            }
            catch { }

            return (userId, name, avatar);
        }
        catch (Exception e)
        {
            PortableLog.Debug(e, "Online", "Graph API 调用失败");
            return (null, null, null);
        }
    }

    private static OnlineLoginResult Fail(string msg) => new() { Success = false, Message = msg };

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value;

        return "";
    }

    private static IEnumerable<string> EnumerateDistinctTokens(params string?[] tokens)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (string.IsNullOrWhiteSpace(token) || !seen.Add(token))
                continue;

            yield return token;
        }
    }

    private sealed record OAuthTokens(string? AccessToken = null, string? RefreshToken = null,
        string? Error = null);

    private sealed record MinecraftProfileResult(JsonObject? Profile, bool NotFound, string? ErrorMessage);

    /// <summary>登出时触发的回调，用于清理主项目档案。</summary>
    public static event Action<string?>? OnLogout;

    public static void Logout()
    {
        var uuid = GetOnlineString("MsUuid");
        OnLogout?.Invoke(uuid);

        SetOnlineString("MsAccessToken", "");
        SetOnlineString("MsOAuthRefreshToken", "");
        SetOnlineString("MsGraphAccessToken", "");
        SetOnlineString("MsGraphRefreshToken", "");
        SetOnlineString("MsId", "");
        SetOnlineString("MsUserName", "");
        SetOnlineString("MsMinecraftProfileName", "");
        SetOnlineString("MsUuid", "");
        SetOnlineString("MsAvatarUrl", "");
        SetOnlineBoolean("MsOwnsMinecraft", false);
        SetOnlineString("MsLastTokenRefresh", "");
        FlushState();
    }

    #region 认证 API

    private static string? AuthXbl(string token)
    {
        try
        {
            var payload = new JsonObject
            {
                ["Properties"] = new JsonObject
                    { ["AuthMethod"] = "RPS", ["SiteName"] = "user.auth.xboxlive.com", ["RpsTicket"] = $"d={token}" },
                ["RelyingParty"] = "http://auth.xboxlive.com", ["TokenType"] = "JWT"
            };
            using var content = new StringContent(payload.ToJsonString(PortableJson.SerializerOptions),
                Encoding.UTF8, "application/json");
            using var r = PortableHttp.Client.PostAsync("https://user.auth.xboxlive.com/user/authenticate", content)
                .GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return PortableJson.ParseObject(r.Content.ReadAsStringAsync().GetAwaiter().GetResult())["Token"]?.ToString();
        }
        catch (Exception e) { PortableLog.Debug(e, "Online", "XBL"); return null; }
    }

    private static JsonObject? AuthXsts(string xblToken, string relyingParty)
    {
        try
        {
            var p = new JsonObject
            {
                ["Properties"] = new JsonObject
                {
                    ["SandboxId"] = "RETAIL",
                    ["UserTokens"] = new JsonArray(JsonValue.Create(xblToken))
                },
                ["RelyingParty"] = relyingParty, ["TokenType"] = "JWT"
            };
            using var content = new StringContent(p.ToJsonString(PortableJson.SerializerOptions),
                Encoding.UTF8, "application/json");
            using var r = PortableHttp.Client.PostAsync("https://xsts.auth.xboxlive.com/xsts/authorize", content)
                .GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return PortableJson.ParseObject(r.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        }
        catch (Exception e) { PortableLog.Debug(e, "Online", "XSTS"); return null; }
    }

    private static string? AuthMc(string xstsToken, string uhs)
    {
        try
        {
            var p = new JsonObject { ["identityToken"] = $"XBL3.0 x={uhs};{xstsToken}" };
            using var content = new StringContent(p.ToJsonString(PortableJson.SerializerOptions),
                Encoding.UTF8, "application/json");
            using var r = PortableHttp.Client
                .PostAsync("https://api.minecraftservices.com/authentication/login_with_xbox", content)
                .GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return PortableJson.ParseObject(r.Content.ReadAsStringAsync().GetAwaiter().GetResult())["access_token"]?.ToString();
        }
        catch (Exception e) { PortableLog.Debug(e, "Online", "MC Auth"); return null; }
    }

    private static MinecraftProfileResult GetProfile(string mcToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            req.Headers.Add("Authorization", $"Bearer {mcToken}");
            using var r = PortableHttp.Client.SendAsync(req).GetAwaiter().GetResult();
            if (r.StatusCode == HttpStatusCode.NotFound)
                return new MinecraftProfileResult(null, true, null);
            if (!r.IsSuccessStatusCode)
            {
                PortableLog.Warn("Online", $"获取 Minecraft 档案失败：HTTP {(int)r.StatusCode}");
                return new MinecraftProfileResult(null, false, Text("Online.Login.MinecraftProfileFailed"));
            }

            return new MinecraftProfileResult(
                PortableJson.ParseObject(r.Content.ReadAsStringAsync().GetAwaiter().GetResult()), false, null);
        }
        catch (Exception e)
        {
            PortableLog.Debug(e, "Online", "Profile");
            return new MinecraftProfileResult(null, false, Text("Online.Login.MinecraftProfileFailed"));
        }
    }

    private static bool CheckOwnership(string mcToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
            req.Headers.Add("Authorization", $"Bearer {mcToken}");
            using var r = PortableHttp.Client.SendAsync(req).GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            var j = PortableJson.ParseObject(r.Content.ReadAsStringAsync().GetAwaiter().GetResult());
            return j["items"]?.AsArray().Any(x =>
                x?["name"]?.ToString() is "product_minecraft" or "game_minecraft") == true;
        }
        catch { return false; }
    }

    #endregion

    private static XboxAuthorization? CreateXboxAuthorization(string accessToken, string relyingParty)
    {
        var xblToken = AuthXbl(accessToken);
        if (xblToken is null)
            return null;

        var xsts = AuthXsts(xblToken, relyingParty);
        var xstsToken = xsts?["Token"]?.ToString();
        var userHash = xsts?["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString();
        return string.IsNullOrWhiteSpace(xstsToken) || string.IsNullOrWhiteSpace(userHash)
            ? null
            : new XboxAuthorization(xstsToken, userHash);
    }
}
