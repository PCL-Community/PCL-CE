// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace PCL.Application.Accounts;

public sealed record ThirdPartyAuthLoginRequest(
    string Server,
    string Username,
    string Password);

public sealed record ThirdPartyAuthLoginResult(
    string Username,
    string Uuid,
    string AccessToken,
    string AuthServer,
    string AuthServerDisplayName);

public sealed class ThirdPartyAuthService(HttpClient? httpClient = null)
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly HttpClient _httpClient = httpClient ?? SharedClient;

    public async Task<ThirdPartyAuthLoginResult> AuthenticateAsync(
        ThirdPartyAuthLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string authServer = NormalizeYggdrasilServer(request.Server);
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("认证服务器、邮箱和密码不能为空。", nameof(request));
        }

        JsonObject payload = new()
        {
            ["agent"] = new JsonObject
            {
                ["name"] = "Minecraft",
                ["version"] = 1
            },
            ["username"] = request.Username,
            ["password"] = request.Password,
            ["requestUser"] = true
        };

        using StringContent content = new(
            payload.ToJsonString(),
            Encoding.UTF8,
            "application/json");
        using HttpResponseMessage response = await _httpClient
            .PostAsync($"{authServer}/authserver/authenticate", content, cancellationToken)
            .ConfigureAwait(false);
        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        JsonObject? json = TryParseObject(responseBody);
        if (!response.IsSuccessStatusCode || json?["error"] is not null)
            throw CreateAuthException(response.StatusCode, json, responseBody);

        string accessToken = json?["accessToken"]?.ToString() ?? "";
        JsonObject? profile = json?["selectedProfile"] as JsonObject ??
                              (json?["availableProfiles"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault();
        string uuid = profile?["id"]?.ToString() ?? "";
        string username = profile?["name"]?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(uuid) ||
            string.IsNullOrWhiteSpace(username))
        {
            throw new InvalidOperationException("认证成功，但服务器没有返回可用的 Minecraft 档案。");
        }

        return new ThirdPartyAuthLoginResult(
            username,
            uuid,
            accessToken,
            authServer,
            GetServerDisplayName(authServer));
    }

    public static string NormalizeYggdrasilServer(string server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(server);
        string normalized = server.Trim().TrimEnd('/');
        if (!normalized.Contains("://", StringComparison.Ordinal))
            normalized = "https://" + normalized;
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("认证服务器地址无效。", nameof(server));
        }

        normalized = uri.ToString().TrimEnd('/');
        if (normalized.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^"/authserver".Length].TrimEnd('/');
        if (!normalized.EndsWith("/api/yggdrasil", StringComparison.OrdinalIgnoreCase))
            normalized += "/api/yggdrasil";
        return normalized;
    }

    private static JsonObject? TryParseObject(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            return JsonNode.Parse(responseBody) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static InvalidOperationException CreateAuthException(
        HttpStatusCode statusCode,
        JsonObject? json,
        string responseBody)
    {
        string? errorMessage = json?["errorMessage"]?.ToString();
        if (!string.IsNullOrWhiteSpace(errorMessage))
            return new InvalidOperationException(errorMessage);

        string? error = json?["error"]?.ToString();
        if (!string.IsNullOrWhiteSpace(error))
            return new InvalidOperationException(error);

        if ((int)statusCode is 401 or 403)
            return new InvalidOperationException("认证服务器拒绝了登录请求。请检查邮箱和密码。");

        string detail = string.IsNullOrWhiteSpace(responseBody)
            ? statusCode.ToString()
            : responseBody;
        return new InvalidOperationException("认证服务器返回了无法识别的响应：" + detail);
    }

    private static string GetServerDisplayName(string authServer)
    {
        if (Uri.TryCreate(authServer, UriKind.Absolute, out Uri? uri))
            return uri.Host;

        return "Authlib-Injector";
    }
}
