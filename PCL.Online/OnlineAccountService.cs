// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Utils;

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
}

public static class OnlineAccountService
{
    private const string GraphScope = "https://graph.microsoft.com/User.Read openid profile offline_access";
    private const string XboxScope = "XboxLive.signin offline_access";

    public static bool IsLoggedIn => !string.IsNullOrEmpty(States.Online.MsUserName);
    public static string? UserName => States.Online.MsUserName;
    public static string? AvatarUrl => States.Online.MsAvatarUrl;
    public static bool OwnsMinecraft => States.Online.MsOwnsMinecraft;

    public static bool EnsureAccountIdentity()
    {
        if (!string.IsNullOrWhiteSpace(States.Online.MsId))
            return true;

        var clientId = Secrets.MSOAuthClientId;
        var refreshToken = States.Online.MsGraphRefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
            refreshToken = States.Online.MsOAuthRefreshToken;
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var graphTokens = ExchangeRefreshToken(clientId, refreshToken, GraphScope);
        if (graphTokens.AccessToken is null)
        {
            LogWrapper.Warn("Online", $"无法补全 Microsoft 账户 ID：{graphTokens.Error}");
            return false;
        }

        var graphProfile = FetchGraphProfile(graphTokens.AccessToken);
        if (string.IsNullOrWhiteSpace(graphProfile.id))
            return false;

        States.Online.MsId = graphProfile.id;
        States.Online.MsGraphAccessToken = graphTokens.AccessToken;
        States.Online.MsGraphRefreshToken = graphTokens.RefreshToken ?? refreshToken;
        States.Online.MsOAuthRefreshToken = graphTokens.RefreshToken ?? refreshToken;
        if (!string.IsNullOrWhiteSpace(graphProfile.name))
            States.Online.MsUserName = graphProfile.name;
        if (!string.IsNullOrWhiteSpace(graphProfile.avatarUrl))
            States.Online.MsAvatarUrl = graphProfile.avatarUrl;
        States.Online.MsLastTokenRefresh = DateTime.Now.ToString("O");
        ConfigService.FlushAll();
        return true;
    }

    public static OnlineLoginResult Login(Func<JsonObject, object?> showLoginDialog)
    {
        try
        {
            var clientId = Secrets.MSOAuthClientId;
            if (string.IsNullOrEmpty(clientId))
                return new OnlineLoginResult
                    { Success = false, Message = Lang.Text("Online.Login.ClientIdMissing") };

            var xboxTokens = Authorize(clientId, XboxScope, Lang.Text("Online.Login.Title"), showLoginDialog);
            if (xboxTokens.Error is not null)
                return new OnlineLoginResult { Success = false, Message = xboxTokens.Error };

            // 一个 access token 只能用于一个资源。首次授权后使用刷新令牌，
            // 静默换取应用已获得同意的 Microsoft Graph token。
            var graphTokens = ExchangeRefreshToken(clientId, xboxTokens.RefreshToken!, GraphScope);
            if (graphTokens.Error is not null)
                LogWrapper.Warn("Online", $"无法静默获取 Microsoft Graph 令牌：{graphTokens.Error}");

            return CompleteLogin(graphTokens, xboxTokens);
        }
        catch (Exception ex) when (ex.Message == "$$")
        {
            return new OnlineLoginResult { Success = false, Message = Lang.Text("Online.Login.Cancelled") };
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Online", "登录失败");
            return new OnlineLoginResult { Success = false, Message = ex.Message };
        }
    }

    private static OAuthTokens Authorize(string clientId, string scope, string title,
        Func<JsonObject, object?> showLoginDialog)
    {
        var body = $"client_id={Uri.EscapeDataString(clientId)}&scope={Uri.EscapeDataString(scope)}";
        JsonObject prepareJson;
        using (var response = HttpRequest
                   .CreatePost("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode")
                   .WithFormContent(body).SendAsync().GetAwaiter().GetResult())
        {
            response.EnsureSuccessStatusCode();
            prepareJson = (JsonObject)JsonCompat.ParseNode(response.AsString())!;
            prepareJson["scope"] = scope;
            prepareJson["login_title"] = title;
        }

        var result = showLoginDialog(prepareJson);
        if (result is Exception ex)
            return new OAuthTokens(Error: ex.Message);
        if (result is not string[] oauthResult || oauthResult.Length < 2)
            return new OAuthTokens(Error: Lang.Text("Online.Login.Cancelled"));

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
            using var response = HttpRequest
                .CreatePost("https://login.microsoftonline.com/consumers/oauth2/v2.0/token")
                .WithFormContent(body).SendAsync().GetAwaiter().GetResult();
            var responseBody = response.AsString();
            if (!response.IsSuccess)
            {
                var errorJson = JsonCompat.ParseNode(responseBody) as JsonObject;
                var error = errorJson?["error"]?.ToString() ?? $"HTTP {(int)response.StatusCode}";
                var description = errorJson?["error_description"]?.ToString();
                return new OAuthTokens(Error: string.IsNullOrEmpty(description)
                    ? error
                    : $"{error}: {description}");
            }

            var result = (JsonObject)JsonCompat.ParseNode(responseBody)!;
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
        if (xblToken is null) return Fail(Lang.Text("Online.Login.XboxFailed"));

        var xsts = AuthXsts(xblToken);
        if (xsts is null) return Fail(Lang.Text("Online.Login.XstsFailed"));
        var xstsToken = xsts["Token"]?.ToString();
        var userHash = xsts["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString();
        if (string.IsNullOrEmpty(xstsToken) || string.IsNullOrEmpty(userHash))
            return Fail(Lang.Text("Online.Login.XstsCredentialMissing"));

        var mcToken = AuthMc(xstsToken, userHash);
        if (mcToken is null) return Fail(Lang.Text("Online.Login.MinecraftAuthFailed"));

        var mcProfile = GetProfile(mcToken);
        if (mcProfile is null) return Fail(Lang.Text("Online.Login.MinecraftProfileFailed"));
        var mcName = mcProfile["name"]?.ToString() ?? Lang.Text("Common.State.Unknown");
        var uuid = mcProfile["id"]?.ToString() ?? "";

        var displayName = graphProfile.name ?? mcName;
        var latestRefreshToken = graphTokens.RefreshToken ?? xboxTokens.RefreshToken!;

        var ownsMc = CheckOwnership(mcToken);

        States.Online.MsAccessToken = mcToken;
        States.Online.MsOAuthRefreshToken = latestRefreshToken;
        States.Online.MsGraphAccessToken = graphTokens.AccessToken ?? "";
        States.Online.MsGraphRefreshToken = latestRefreshToken;
        States.Online.MsId = graphProfile.id ?? "";
        States.Online.MsUserName = displayName;
        States.Online.MsMinecraftProfileName = mcName;
        States.Online.MsUuid = uuid;
        States.Online.MsAvatarUrl = graphProfile.avatarUrl ?? "";
        States.Online.MsOwnsMinecraft = ownsMc;
        States.Online.MsLastTokenRefresh = DateTime.Now.ToString("O");
        ConfigService.FlushAll();

        return new OnlineLoginResult
        {
            Success = true,
            Message = Lang.Text(ownsMc ? "Online.Login.SuccessOwned" : "Online.Login.SuccessNotOwned", displayName),
            MsId = graphProfile.id,
            UserName = mcName, DisplayName = displayName, MinecraftProfileName = mcName, Uuid = uuid, AccessToken = mcToken,
            RefreshToken = latestRefreshToken,
            OwnsMinecraft = ownsMc
        };
    }

    private static (string? id, string? name, string? avatarUrl) FetchGraphProfile(string msToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            req.Headers.Add("Authorization", $"Bearer {msToken}");
            using var r = NetworkService.GetClient().SendAsync(req).GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            var json = (JsonObject)JsonCompat.ParseNode(r.AsString())!;
            var name = json["displayName"]?.ToString();
            var userId = json["id"]?.ToString();

            string? avatar = null;
            try
            {
                using var photoReq = new HttpRequestMessage(HttpMethod.Get,
                    "https://graph.microsoft.com/v1.0/me/photo/$value");
                photoReq.Headers.Add("Authorization", $"Bearer {msToken}");
                using var photoResp = NetworkService.GetClient()
                    .SendAsync(photoReq).GetAwaiter().GetResult();
                if (photoResp.IsSuccessStatusCode)
                {
                    var bytes = photoResp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PCL_N", "Avatars");
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
            LogWrapper.Debug(e, "Online", "Graph API 调用失败");
            return (null, null, null);
        }
    }

    private static OnlineLoginResult Fail(string msg) => new() { Success = false, Message = msg };

    private sealed record OAuthTokens(string? AccessToken = null, string? RefreshToken = null,
        string? Error = null);

    /// <summary>登出时触发的回调，用于清理主项目档案。</summary>
    public static event Action<string?>? OnLogout;

    public static void Logout()
    {
        var uuid = States.Online.MsUuid;
        OnLogout?.Invoke(uuid);

        States.Online.MsAccessToken = "";
        States.Online.MsOAuthRefreshToken = "";
        States.Online.MsGraphAccessToken = "";
        States.Online.MsGraphRefreshToken = "";
        States.Online.MsId = "";
        States.Online.MsUserName = "";
        States.Online.MsMinecraftProfileName = "";
        States.Online.MsUuid = "";
        States.Online.MsAvatarUrl = "";
        States.Online.MsOwnsMinecraft = false;
        States.Online.MsLastTokenRefresh = "";
        ConfigService.FlushAll();
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
            using var r = HttpRequest.CreatePost("https://user.auth.xboxlive.com/user/authenticate")
                .WithJsonContent(payload).SendAsync().GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return ((JsonObject)JsonCompat.ParseNode(r.AsString())!)["Token"]?.ToString();
        }
        catch (Exception e) { LogWrapper.Debug(e, "Online", "XBL"); return null; }
    }

    private static JsonObject? AuthXsts(string xblToken)
    {
        try
        {
            var p = new JsonObject
            {
                ["Properties"] = new JsonObject { ["SandboxId"] = "RETAIL", ["UserTokens"] = new JsonArray { xblToken } },
                ["RelyingParty"] = "rp://api.minecraftservices.com/", ["TokenType"] = "JWT"
            };
            using var r = HttpRequest.CreatePost("https://xsts.auth.xboxlive.com/xsts/authorize")
                .WithJsonContent(p).SendAsync().GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return (JsonObject)JsonCompat.ParseNode(r.AsString())!;
        }
        catch (Exception e) { LogWrapper.Debug(e, "Online", "XSTS"); return null; }
    }

    private static string? AuthMc(string xstsToken, string uhs)
    {
        try
        {
            var p = new JsonObject { ["identityToken"] = $"XBL3.0 x={uhs};{xstsToken}" };
            using var r = HttpRequest.CreatePost("https://api.minecraftservices.com/authentication/login_with_xbox")
                .WithJsonContent(p).SendAsync().GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return ((JsonObject)JsonCompat.ParseNode(r.AsString())!)["access_token"]?.ToString();
        }
        catch (Exception e) { LogWrapper.Debug(e, "Online", "MC Auth"); return null; }
    }

    private static JsonObject? GetProfile(string mcToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            req.Headers.Add("Authorization", $"Bearer {mcToken}");
            using var r = NetworkService.GetClient().SendAsync(req).GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            return (JsonObject)JsonCompat.ParseNode(r.AsString())!;
        }
        catch (Exception e) { LogWrapper.Debug(e, "Online", "Profile"); return null; }
    }

    private static bool CheckOwnership(string mcToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
            req.Headers.Add("Authorization", $"Bearer {mcToken}");
            using var r = NetworkService.GetClient().SendAsync(req).GetAwaiter().GetResult();
            r.EnsureSuccessStatusCode();
            var j = (JsonObject)JsonCompat.ParseNode(r.AsString())!;
            return j["items"]?.AsArray() is { Count: > 0 };
        }
        catch { return false; }
    }

    #endregion
}
