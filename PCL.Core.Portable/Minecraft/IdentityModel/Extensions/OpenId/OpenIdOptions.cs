// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

public record OpenIdOptions
{
    /// <summary>
    /// OpenID discovery document address.
    /// </summary>
    public required string OpenIdDiscoveryAddress { get; set; }

    /// <summary>
    /// OAuth client identifier.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Restricts the client to device-code authorization.
    /// </summary>
    public bool OnlyDeviceAuthorize { get; set; }

    /// <summary>
    /// Authorization-code redirect URI.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Headers applied to discovery, JWKS, and OAuth requests.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Enables PKCE for authorization-code requests.
    /// </summary>
    public bool EnablePkceSupport { get; set; } = true;

    /// <summary>
    /// Provides an HTTP client whose lifetime is managed by the caller.
    /// </summary>
    public required Func<HttpClient> GetClient { get; set; }

    /// <summary>
    /// Loaded OpenID metadata.
    /// </summary>
    public OpenIdMetadata? Meta { get; protected set; }

    public virtual async Task InitializeAsync(CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(OpenIdDiscoveryAddress);
        Meta = await GetJsonAsync(
                OpenIdDiscoveryAddress,
                OpenIdJsonContext.Default.OpenIdMetadata,
                token)
            .ConfigureAwait(false);
    }

    public async Task<JsonWebKeyData> GetSignatureKeyAsync(
        string kid,
        CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kid);
        if (string.IsNullOrWhiteSpace(Meta?.JwksUri))
            throw new IdentityModelConfigurationException(
                "请先调用 InitializeAsync() 加载 OpenID 元数据");

        var keySet = await GetJsonAsync(
                Meta.JwksUri,
                OpenIdJsonContext.Default.JsonWebKeys,
                token)
            .ConfigureAwait(false);
        foreach (var key in keySet.Keys)
            if (string.Equals(key.KeyId, kid, StringComparison.Ordinal))
                return key;

        throw new IdentityModelConfigurationException($"找不到匹配的 JWK：{kid}");
    }

    public virtual Task<OAuthClientOptions> BuildOAuthOptionsAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Meta is null)
            throw new IdentityModelConfigurationException(
                "请先调用 InitializeAsync() 加载 OpenID 元数据");
        if (string.IsNullOrWhiteSpace(Meta.TokenEndpoint))
            throw new IdentityModelConfigurationException(
                "OpenID 元数据缺少 TokenEndpoint");
        if (!OnlyDeviceAuthorize && string.IsNullOrWhiteSpace(RedirectUri))
            throw new IdentityModelConfigurationException(
                "授权代码流需要设置 RedirectUri");

        return Task.FromResult(new OAuthClientOptions
        {
            GetClient = GetClient,
            ClientId = ClientId,
            RedirectUri = OnlyDeviceAuthorize ? string.Empty : RedirectUri!,
            Headers = Headers is null
                ? null
                : new Dictionary<string, string>(Headers, StringComparer.Ordinal),
            Meta = new EndpointMeta
            {
                AuthorizeEndpoint = Meta.AuthorizationEndpoint ?? string.Empty,
                DeviceEndpoint = Meta.DeviceAuthorizationEndpoint ?? string.Empty,
                TokenEndpoint = Meta.TokenEndpoint
            }
        });
    }

    protected async Task<T> GetJsonAsync<T>(
        string address,
        JsonTypeInfo<T> typeInfo,
        CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        if (Headers is not null)
            foreach (var header in Headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        var client = GetClient()
                     ?? throw new IdentityModelConfigurationException(
                         "HttpClient provider returned null");
        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonSerializer
                   .DeserializeAsync(stream, typeInfo, cancellationToken)
                   .ConfigureAwait(false)
               ?? throw new IdentityModelConfigurationException(
                   $"无法解析 OpenID 元数据：{address}");
    }
}
