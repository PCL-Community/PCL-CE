// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Broker;
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
    public bool HasMinecraftProfile { get; init; }
    public bool MinecraftProfileMissing { get; init; }
}

public sealed record XboxAuthorization(string XstsToken, string UserHash);

public static class OnlineAccountService
{
    private const string GraphScope = "https://graph.microsoft.com/User.Read openid profile offline_access";
    private const string XboxScope = "XboxLive.signin offline_access";
    private static readonly string[] MsalXboxScopes = ["XboxLive.signin"];
    private static readonly string[] MsalGraphScopes = ["User.Read"];

    public static bool IsLoggedIn => !string.IsNullOrEmpty(States.Online.MsUserName);
    public static string? UserName => States.Online.MsUserName;
    public static string? AvatarUrl => States.Online.MsAvatarUrl;
    public static bool OwnsMinecraft => States.Online.MsOwnsMinecraft;

    public static XboxAuthorization? GetXboxAuthorization(string relyingParty = "http://xboxlive.com")
    {
        var clientId = Secrets.MSOAuthClientId;
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            foreach (var refreshToken in EnumerateDistinctTokens(
                         States.Online.MsOAuthRefreshToken,
                         States.Online.MsGraphRefreshToken))
            {
                var tokens = ExchangeRefreshToken(clientId, refreshToken, XboxScope);
                if (tokens.AccessToken is null)
                {
                    LogWrapper.Warn("Online", $"无法刷新 Xbox 令牌：{tokens.Error}");
                    continue;
                }

                States.Online.MsOAuthRefreshToken = tokens.RefreshToken ?? refreshToken;
                States.Online.MsLastTokenRefresh = DateTime.Now.ToString("O");
                ConfigService.FlushAll();

                var authorization = CreateXboxAuthorization(tokens.AccessToken, relyingParty);
                if (authorization is not null)
                    return authorization;
            }
        }

        return GetXboxAuthorizationWithWamSilent(relyingParty);
    }

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
        if (string.IsNullOrWhiteSpace(States.Online.MsOAuthRefreshToken))
            States.Online.MsOAuthRefreshToken = refreshToken;
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
        return LoginCore(showLoginDialog, Lang.Text("Online.Login.Title"));
    }

    public static OnlineLoginResult LoginWithWindowsAccount(Func<JsonObject, object?> showLoginDialog)
    {
        return LoginCore(showLoginDialog, Lang.Text("Online.Login.WindowsTitle"));
    }

    public static async Task<OnlineLoginResult> LoginWithWindowsAccountAsync(IntPtr parentWindowHandle,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
            return new OnlineLoginResult { Success = false, Message = Lang.Text("Online.Login.WindowsUnsupported") };
        if (string.IsNullOrWhiteSpace(Secrets.MSOAuthClientId))
            return new OnlineLoginResult { Success = false, Message = Lang.Text("Online.Login.ClientIdMissing") };

        try
        {
            var app = BuildWamApplication(parentWindowHandle);
            var xboxResult = await app.AcquireTokenInteractive(MsalXboxScopes)
                .WithAccount(PublicClientApplication.OperatingSystemAccount)
                .WithParentActivityOrWindow(parentWindowHandle)
                .ExecuteAsync(cancellationToken)
                .ConfigureAwait(true);

            AuthenticationResult? graphResult = null;
            try
            {
                graphResult = await app.AcquireTokenSilent(MsalGraphScopes, xboxResult.Account)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(true);
            }
            catch (MsalUiRequiredException)
            {
                graphResult = await app.AcquireTokenInteractive(MsalGraphScopes)
                    .WithAccount(xboxResult.Account)
                    .WithParentActivityOrWindow(parentWindowHandle)
                    .ExecuteAsync(cancellationToken)
                    .ConfigureAwait(true);
            }

            return await Task.Run(() => CompleteLogin(
                    new OAuthTokens(graphResult.AccessToken),
                    new OAuthTokens(xboxResult.AccessToken)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Online", "WAM 登录失败");
            return new OnlineLoginResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private static OnlineLoginResult LoginCore(Func<JsonObject, object?> showLoginDialog, string title)
    {
        try
        {
            var clientId = Secrets.MSOAuthClientId;
            if (string.IsNullOrEmpty(clientId))
                return new OnlineLoginResult
                    { Success = false, Message = Lang.Text("Online.Login.ClientIdMissing") };

            var xboxTokens = Authorize(clientId, XboxScope, title, showLoginDialog);
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

        var xsts = AuthXsts(xblToken, "rp://api.minecraftservices.com/");
        if (xsts is null) return Fail(Lang.Text("Online.Login.XstsFailed"));
        var xstsToken = xsts["Token"]?.ToString();
        var userHash = xsts["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString();
        if (string.IsNullOrEmpty(xstsToken) || string.IsNullOrEmpty(userHash))
            return Fail(Lang.Text("Online.Login.XstsCredentialMissing"));

        var mcToken = AuthMc(xstsToken, userHash);
        if (mcToken is null) return Fail(Lang.Text("Online.Login.MinecraftAuthFailed"));

        var mcProfileResult = GetProfile(mcToken);
        if (mcProfileResult.ErrorMessage is not null)
            return Fail(mcProfileResult.ErrorMessage);

        var mcProfile = mcProfileResult.Profile;
        var hasMcProfile = mcProfile is not null;
        var mcName = hasMcProfile
            ? mcProfile?["name"]?.ToString() ?? Lang.Text("Common.State.Unknown")
            : "";
        var uuid = hasMcProfile ? mcProfile?["id"]?.ToString() ?? "" : "";

        var displayName = FirstNonEmpty(graphProfile.name, mcName, graphProfile.id,
            Lang.Text("Online.Login.MicrosoftAccount"));
        var xboxRefreshToken = FirstNonEmpty(
            xboxTokens.RefreshToken,
            States.Online.MsOAuthRefreshToken,
            States.Online.MsGraphRefreshToken);
        var graphRefreshToken = FirstNonEmpty(
            graphTokens.RefreshToken,
            States.Online.MsGraphRefreshToken,
            States.Online.MsOAuthRefreshToken);

        var ownsMc = CheckOwnership(mcToken);

        States.Online.MsAccessToken = mcToken;
        States.Online.MsOAuthRefreshToken = xboxRefreshToken;
        States.Online.MsGraphAccessToken = graphTokens.AccessToken ?? "";
        States.Online.MsGraphRefreshToken = graphRefreshToken;
        States.Online.MsId = graphProfile.id ?? "";
        States.Online.MsUserName = displayName;
        States.Online.MsMinecraftProfileName = mcName;
        States.Online.MsUuid = uuid;
        States.Online.MsAvatarUrl = graphProfile.avatarUrl ?? "";
        States.Online.MsOwnsMinecraft = ownsMc;
        States.Online.MsLastTokenRefresh = DateTime.Now.ToString("O");
        ConfigService.FlushAll();

        var messageKey = (hasMcProfile, ownsMc) switch
        {
            (true, true) => "Online.Login.SuccessOwned",
            (false, true) => "Online.Login.SuccessProfileMissing",
            _ => "Online.Login.SuccessNotOwned"
        };

        return new OnlineLoginResult
        {
            Success = true,
            Message = Lang.Text(messageKey, displayName),
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

    private static JsonObject? AuthXsts(string xblToken, string relyingParty)
    {
        try
        {
            var p = new JsonObject
            {
                ["Properties"] = new JsonObject { ["SandboxId"] = "RETAIL", ["UserTokens"] = new JsonArray { xblToken } },
                ["RelyingParty"] = relyingParty, ["TokenType"] = "JWT"
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

    private static MinecraftProfileResult GetProfile(string mcToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            req.Headers.Add("Authorization", $"Bearer {mcToken}");
            using var r = NetworkService.GetClient().SendAsync(req).GetAwaiter().GetResult();
            if (r.StatusCode == HttpStatusCode.NotFound)
                return new MinecraftProfileResult(null, true, null);
            if (!r.IsSuccessStatusCode)
            {
                LogWrapper.Warn("Online", $"获取 Minecraft 档案失败：HTTP {(int)r.StatusCode}");
                return new MinecraftProfileResult(null, false, Lang.Text("Online.Login.MinecraftProfileFailed"));
            }

            return new MinecraftProfileResult((JsonObject)JsonCompat.ParseNode(r.AsString())!, false, null);
        }
        catch (Exception e)
        {
            LogWrapper.Debug(e, "Online", "Profile");
            return new MinecraftProfileResult(null, false, Lang.Text("Online.Login.MinecraftProfileFailed"));
        }
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
            return j["items"]?.AsArray().Any(x =>
                x?["name"]?.ToString() is "product_minecraft" or "game_minecraft") == true;
        }
        catch { return false; }
    }

    #endregion

    private static IPublicClientApplication BuildWamApplication(IntPtr parentWindowHandle)
    {
        var brokerOptions = new BrokerOptions(BrokerOptions.OperatingSystems.Windows)
        {
            Title = "PCL N",
            ListOperatingSystemAccounts = true,
            MsaPassthrough = true
        };

        var builder = PublicClientApplicationBuilder
            .Create(Secrets.MSOAuthClientId)
            .WithAuthority("https://login.microsoftonline.com/consumers")
            .WithDefaultRedirectUri()
            .WithBroker(brokerOptions);

        if (parentWindowHandle != IntPtr.Zero)
            builder = builder.WithParentActivityOrWindow(() => parentWindowHandle);

        return builder.Build();
    }

    private static XboxAuthorization? GetXboxAuthorizationWithWamSilent(string relyingParty)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(Secrets.MSOAuthClientId))
            return null;

        try
        {
            var app = BuildWamApplication(IntPtr.Zero);
            var result = app.AcquireTokenSilent(MsalXboxScopes, PublicClientApplication.OperatingSystemAccount)
                .ExecuteAsync()
                .GetAwaiter()
                .GetResult();
            return CreateXboxAuthorization(result.AccessToken, relyingParty);
        }
        catch (Exception ex)
        {
            LogWrapper.Debug(ex, "Online", "WAM 静默获取 Xbox 令牌失败");
            return null;
        }
    }

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
