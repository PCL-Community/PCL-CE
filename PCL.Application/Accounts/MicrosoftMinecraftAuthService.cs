// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using System.Text.Json;
using PCL.Core.IO.Net;

namespace PCL.Application.Accounts;

public sealed record MicrosoftDeviceCodeInfo(
    string DeviceCode,
    string UserCode,
    string VerificationUri,
    string? VerificationUriComplete,
    string Message,
    TimeSpan ExpiresIn,
    TimeSpan PollInterval);

public sealed record MicrosoftMinecraftLoginResult(
    string Username,
    string Uuid,
    string AccessToken,
    string RefreshToken,
    string? SkinAddress,
    bool OwnsMinecraft);

public interface IMicrosoftMinecraftAuthService
{
    Task<MicrosoftDeviceCodeInfo> RequestDeviceCodeAsync(
        string clientId,
        CancellationToken cancellationToken = default);

    Task<MicrosoftMinecraftLoginResult> CompleteDeviceLoginAsync(
        string clientId,
        MicrosoftDeviceCodeInfo deviceCode,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);

    Task<MicrosoftMinecraftLoginResult> RefreshAsync(
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken = default);
}

public sealed class MicrosoftMinecraftAuthService : IMicrosoftMinecraftAuthService
{
    private const string Scope = "XboxLive.signin offline_access";
    private const string DeviceCodeEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private readonly HttpClient _client;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public MicrosoftMinecraftAuthService()
        : this(PortableHttp.Client)
    {
    }

    public MicrosoftMinecraftAuthService(
        HttpClient client,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _delay = delay ?? Task.Delay;
    }

    public static string ResolveClientId() =>
        FirstNonEmpty(
            Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"),
            Environment.GetEnvironmentVariable("MS_CLIENT_ID"));

    public async Task<MicrosoftDeviceCodeInfo> RequestDeviceCodeAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        using HttpResponseMessage response = await PostFormAsync(
                DeviceCodeEndpoint,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["client_id"] = clientId,
                    ["scope"] = Scope
                },
                cancellationToken)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body, "获取 Microsoft 设备登录代码失败");
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        string deviceCode = RequiredString(root, "device_code");
        string userCode = RequiredString(root, "user_code");
        string verificationUri = RequiredString(root, "verification_uri");
        int expiresIn = ReadInteger(root, "expires_in", 900);
        int interval = ReadInteger(root, "interval", 5);
        return new MicrosoftDeviceCodeInfo(
            deviceCode,
            userCode,
            verificationUri,
            ReadOptionalString(root, "verification_uri_complete"),
            ReadOptionalString(root, "message") ?? $"请打开 {verificationUri} 并输入代码 {userCode}。",
            TimeSpan.FromSeconds(Math.Max(1, expiresIn)),
            TimeSpan.FromSeconds(Math.Max(1, interval)));
    }

    public async Task<MicrosoftMinecraftLoginResult> CompleteDeviceLoginAsync(
        string clientId,
        MicrosoftDeviceCodeInfo deviceCode,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(deviceCode);
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow + deviceCode.ExpiresIn;
        TimeSpan interval = deviceCode.PollInterval;
        OAuthTokenResult tokens;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= expiresAt)
                throw new TimeoutException("Microsoft 登录代码已过期，请重新开始登录。");
            await _delay(interval, cancellationToken).ConfigureAwait(false);

            OAuthTokenResponse response = await RequestTokenAsync(
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["client_id"] = clientId,
                        ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                        ["device_code"] = deviceCode.DeviceCode,
                        ["scope"] = Scope
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (response.Tokens is { } completed)
            {
                tokens = completed;
                break;
            }

            switch (response.Error)
            {
                case "authorization_pending":
                    break;
                case "slow_down":
                    interval += TimeSpan.FromSeconds(5);
                    break;
                case "authorization_declined":
                    throw new InvalidOperationException("Microsoft 登录已被拒绝。");
                case "expired_token":
                    throw new TimeoutException("Microsoft 登录代码已过期，请重新开始登录。");
                default:
                    throw new InvalidOperationException(response.ErrorDescription ?? response.Error ?? "Microsoft 登录失败。");
            }

            double elapsed = 1d - Math.Max(0d, (expiresAt - DateTimeOffset.UtcNow).TotalSeconds) /
                Math.Max(1d, deviceCode.ExpiresIn.TotalSeconds);
            progress?.Report(Math.Clamp(0.08d + elapsed * 0.32d, 0.08d, 0.4d));
        }

        progress?.Report(0.45d);
        return await CompleteMinecraftLoginAsync(tokens, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MicrosoftMinecraftLoginResult> RefreshAsync(
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        OAuthTokenResponse response = await RequestTokenAsync(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["client_id"] = clientId,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["scope"] = Scope
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (response.Tokens is not { } tokens)
            throw new InvalidOperationException(response.ErrorDescription ?? response.Error ?? "刷新 Microsoft 登录失败。");
        return await CompleteMinecraftLoginAsync(tokens, null, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MicrosoftMinecraftLoginResult> CompleteMinecraftLoginAsync(
        OAuthTokenResult microsoftTokens,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        XboxLiveToken xboxLive = await AuthenticateXboxLiveAsync(
                microsoftTokens.AccessToken,
                cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(0.58d);
        XboxLiveToken xsts = await AuthorizeXstsAsync(xboxLive.Token, cancellationToken).ConfigureAwait(false);
        progress?.Report(0.7d);
        string minecraftAccessToken = await AuthenticateMinecraftAsync(
                xsts.UserHash,
                xsts.Token,
                cancellationToken)
            .ConfigureAwait(false);
        progress?.Report(0.82d);
        (string username, string uuid, string? skinAddress) = await GetMinecraftProfileAsync(
                minecraftAccessToken,
                cancellationToken)
            .ConfigureAwait(false);
        bool ownsMinecraft = await CheckOwnershipAsync(minecraftAccessToken, cancellationToken).ConfigureAwait(false);
        progress?.Report(1d);
        return new MicrosoftMinecraftLoginResult(
            username,
            uuid,
            minecraftAccessToken,
            microsoftTokens.RefreshToken,
            skinAddress,
            ownsMinecraft);
    }

    private async Task<OAuthTokenResponse> RequestTokenAsync(
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await PostFormAsync(TokenEndpoint, form, cancellationToken)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        string? accessToken = ReadOptionalString(root, "access_token");
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(accessToken))
        {
            return new OAuthTokenResponse(
                new OAuthTokenResult(
                    accessToken,
                    FirstNonEmpty(ReadOptionalString(root, "refresh_token"), form.GetValueOrDefault("refresh_token"))),
                null,
                null);
        }
        return new OAuthTokenResponse(
            null,
            ReadOptionalString(root, "error") ?? $"HTTP {(int)response.StatusCode}",
            ReadOptionalString(root, "error_description"));
    }

    private async Task<XboxLiveToken> AuthenticateXboxLiveAsync(
        string microsoftAccessToken,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await PostJsonAsync(
                "https://user.auth.xboxlive.com/user/authenticate",
                $$"""
                {"Properties":{"AuthMethod":"RPS","SiteName":"user.auth.xboxlive.com","RpsTicket":"d={{EscapeJson(microsoftAccessToken)}}"},"RelyingParty":"http://auth.xboxlive.com","TokenType":"JWT"}
                """,
                cancellationToken)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body, "Xbox Live 登录失败");
        return ReadXboxToken(body, "Xbox Live 登录响应缺少令牌。");
    }

    private async Task<XboxLiveToken> AuthorizeXstsAsync(
        string xboxLiveToken,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await PostJsonAsync(
                "https://xsts.auth.xboxlive.com/xsts/authorize",
                $$"""
                {"Properties":{"SandboxId":"RETAIL","UserTokens":["{{EscapeJson(xboxLiveToken)}}"]},"RelyingParty":"rp://api.minecraftservices.com/","TokenType":"JWT"}
                """,
                cancellationToken)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw CreateXstsException(response.StatusCode, body);
        return ReadXboxToken(body, "XSTS 响应缺少令牌或用户标识。");
    }

    private async Task<string> AuthenticateMinecraftAsync(
        string userHash,
        string xstsToken,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await PostJsonAsync(
                "https://api.minecraftservices.com/authentication/login_with_xbox",
                $$"""{"identityToken":"XBL3.0 x={{EscapeJson(userHash)}};{{EscapeJson(xstsToken)}}"}""",
                cancellationToken)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body, "Minecraft 服务登录失败");
        using JsonDocument document = JsonDocument.Parse(body);
        return RequiredString(document.RootElement, "access_token");
    }

    private async Task<(string Username, string Uuid, string? SkinAddress)> GetMinecraftProfileAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("此 Microsoft 账户尚未创建 Minecraft Java 版档案。");
        EnsureSuccess(response, body, "获取 Minecraft 档案失败");
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        string? skin = null;
        if (root.TryGetProperty("skins", out JsonElement skins) && skins.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in skins.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                    continue;
                string? url = ReadOptionalString(entry, "url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    skin = url;
                    if (string.Equals(ReadOptionalString(entry, "state"), "ACTIVE", StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }
        }
        return (RequiredString(root, "name"), RequiredString(root, "id"), skin);
    }

    private async Task<bool> CheckOwnershipAsync(string accessToken, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, "https://api.minecraftservices.com/entitlements/mcstore");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return false;
        string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("items", out JsonElement items) || items.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement item in items.EnumerateArray())
        {
            string? name = item.ValueKind == JsonValueKind.Object ? ReadOptionalString(item, "name") : null;
            if (name is "product_minecraft" or "game_minecraft")
                return true;
        }
        return false;
    }

    private Task<HttpResponseMessage> PostFormAsync(
        string endpoint,
        Dictionary<string, string> form,
        CancellationToken cancellationToken) =>
        _client.PostAsync(endpoint, new FormUrlEncodedContent(form), cancellationToken);

    private Task<HttpResponseMessage> PostJsonAsync(
        string endpoint,
        string json,
        CancellationToken cancellationToken) =>
        _client.PostAsync(endpoint, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);

    private static XboxLiveToken ReadXboxToken(string body, string missingMessage)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        string token = RequiredString(root, "Token");
        string? userHash = null;
        if (root.TryGetProperty("DisplayClaims", out JsonElement claims) &&
            claims.TryGetProperty("xui", out JsonElement xui) &&
            xui.ValueKind == JsonValueKind.Array &&
            xui.GetArrayLength() > 0)
        {
            userHash = ReadOptionalString(xui[0], "uhs");
        }
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userHash))
            throw new InvalidDataException(missingMessage);
        return new XboxLiveToken(token, userHash);
    }

    private static Exception CreateXstsException(HttpStatusCode statusCode, string body)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            long xerr = document.RootElement.TryGetProperty("XErr", out JsonElement value) && value.TryGetInt64(out long result)
                ? result
                : 0L;
            return xerr switch
            {
                2148916233 => new InvalidOperationException("此 Microsoft 账户尚未创建 Xbox 档案。请先登录 Xbox 官网完成设置。"),
                2148916235 or 2148916236 => new InvalidOperationException("Xbox 服务在此账户所在地区不可用。"),
                2148916238 => new InvalidOperationException("此账户受家庭安全设置限制，无法授权 Minecraft。"),
                _ => new HttpRequestException($"XSTS 授权失败（HTTP {(int)statusCode}，XErr {xerr}）。")
            };
        }
        catch (JsonException)
        {
            return new HttpRequestException($"XSTS 授权失败（HTTP {(int)statusCode}）。");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;
        string detail = string.Empty;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            detail = FirstNonEmpty(
                ReadOptionalString(document.RootElement, "error_description"),
                ReadOptionalString(document.RootElement, "errorMessage"),
                ReadOptionalString(document.RootElement, "message"));
        }
        catch (JsonException)
        {
        }
        throw new HttpRequestException(string.IsNullOrWhiteSpace(detail)
            ? $"{operation}（HTTP {(int)response.StatusCode}）。"
            : $"{operation}：{detail}");
    }

    private static string RequiredString(JsonElement element, string propertyName) =>
        ReadOptionalString(element, propertyName) is { Length: > 0 } value
            ? value
            : throw new InvalidDataException($"响应缺少字段 {propertyName}。");

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int ReadInteger(JsonElement element, string propertyName, int fallback) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out JsonElement value) &&
        value.TryGetInt32(out int result)
            ? result
            : fallback;

    private static string EscapeJson(string value) =>
        JsonEncodedText.Encode(value).ToString();

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return string.Empty;
    }

    private sealed record OAuthTokenResult(string AccessToken, string RefreshToken);

    private sealed record OAuthTokenResponse(
        OAuthTokenResult? Tokens,
        string? Error,
        string? ErrorDescription);

    private sealed record XboxLiveToken(string Token, string UserHash);
}
