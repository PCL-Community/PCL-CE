// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PCL.Core.Minecraft.IdentityModel.OAuth;

/// <summary>
/// Cross-platform OAuth 2.0 authorization-code, device-code, and refresh-token client.
/// </summary>
public sealed class SimpleOAuthClient(OAuthClientOptions options) : IOAuthClient
{
    public string GetAuthorizeUrl(
        string[] scopes,
        string state,
        Dictionary<string, string>? extData = null)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Meta.AuthorizeEndpoint);

        var builder = new StringBuilder(options.Meta.AuthorizeEndpoint);
        var hasQuery = options.Meta.AuthorizeEndpoint.Contains('?', StringComparison.Ordinal);
        AppendQuery(builder, "response_type", "code", ref hasQuery);
        AppendQuery(builder, "scope", string.Join(' ', scopes), ref hasQuery);
        AppendQuery(builder, "redirect_uri", options.RedirectUri, ref hasQuery);
        AppendQuery(builder, "client_id", options.ClientId, ref hasQuery);
        AppendQuery(builder, "state", state, ref hasQuery);
        if (extData is not null)
            foreach (var pair in extData)
                AppendQuery(builder, pair.Key, pair.Value, ref hasQuery);
        return builder.ToString();
    }

    public Task<AuthorizeResult?> AuthorizeWithCodeAsync(
        string code,
        CancellationToken token,
        Dictionary<string, string>? extData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var form = CopyForm(extData);
        form["client_id"] = options.ClientId;
        form["grant_type"] = "authorization_code";
        form["code"] = code;
        if (!string.IsNullOrWhiteSpace(options.RedirectUri))
            form["redirect_uri"] = options.RedirectUri;
        return PostFormAsync(
            options.Meta.TokenEndpoint,
            form,
            OAuthJsonContext.Default.AuthorizeResult,
            token);
    }

    public Task<DeviceCodeData?> GetCodePairAsync(
        string[] scopes,
        CancellationToken token,
        Dictionary<string, string>? extData = null)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Meta.DeviceEndpoint);
        var form = CopyForm(extData);
        form["scope"] = string.Join(' ', scopes);
        form["client_id"] = options.ClientId;
        return PostFormAsync(
            options.Meta.DeviceEndpoint,
            form,
            OAuthJsonContext.Default.DeviceCodeData,
            token);
    }

    public Task<AuthorizeResult?> AuthorizeWithDeviceAsync(
        DeviceCodeData data,
        CancellationToken token,
        Dictionary<string, string>? extData = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.IsError)
            throw new IdentityModelAuthenticationException(data.Error, data.ErrorDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.DeviceCode);

        var form = CopyForm(extData);
        form["client_id"] = options.ClientId;
        form["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code";
        form["device_code"] = data.DeviceCode;
        return PostFormAsync(
            options.Meta.TokenEndpoint,
            form,
            OAuthJsonContext.Default.AuthorizeResult,
            token);
    }

    public Task<AuthorizeResult?> AuthorizeWithSilentAsync(
        AuthorizeResult data,
        CancellationToken token,
        Dictionary<string, string>? extData = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.IsError)
            throw new IdentityModelAuthenticationException(data.Error, data.ErrorDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.RefreshToken);

        var form = CopyForm(extData);
        form["refresh_token"] = data.RefreshToken;
        form["grant_type"] = "refresh_token";
        form["client_id"] = options.ClientId;
        return PostFormAsync(
            options.Meta.TokenEndpoint,
            form,
            OAuthJsonContext.Default.AuthorizeResult,
            token);
    }

    private async Task<T?> PostFormAsync<T>(
        string endpoint,
        Dictionary<string, string> form,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(form)
        };
        if (options.Headers is not null)
            foreach (var header in options.Headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        using var response = await options.GetClient()
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var result = await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false);
        if (result is null)
            response.EnsureSuccessStatusCode();
        return result;
    }

    private static Dictionary<string, string> CopyForm(Dictionary<string, string>? source) =>
        source is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(source, StringComparer.Ordinal);

    private static void AppendQuery(StringBuilder builder, string key, string value, ref bool hasQuery)
    {
        builder.Append(hasQuery ? '&' : '?');
        hasQuery = true;
        builder.Append(Uri.EscapeDataString(key))
            .Append('=')
            .Append(Uri.EscapeDataString(value));
    }
}
