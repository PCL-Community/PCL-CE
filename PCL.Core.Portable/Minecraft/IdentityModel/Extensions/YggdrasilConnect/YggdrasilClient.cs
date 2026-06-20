// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

/// <summary>
/// Portable Yggdrasil Connect client built on the OpenID Connect flow.
/// </summary>
public sealed class YggdrasilClient(YggdrasilOptions options) : IOAuthClient
{
    private OpenIdClient? _client;

    public async Task InitializeAsync(CancellationToken token)
    {
        var client = new OpenIdClient(options);
        await client.InitializeAsync(token, checkAddress: true).ConfigureAwait(false);
        _client = client;
    }

    public string GetAuthorizeUrl(
        string[] scopes,
        string state,
        Dictionary<string, string>? extData = null) =>
        GetClient().GetAuthorizeUrl(scopes, state, extData);

    public Task<AuthorizeResult?> AuthorizeWithCodeAsync(
        string code,
        CancellationToken token,
        Dictionary<string, string>? extData = null) =>
        GetClient().AuthorizeWithCodeAsync(code, token, extData);

    public Task<DeviceCodeData?> GetCodePairAsync(
        string[] scopes,
        CancellationToken token,
        Dictionary<string, string>? extData = null) =>
        GetClient().GetCodePairAsync(scopes, token, extData);

    public Task<AuthorizeResult?> AuthorizeWithDeviceAsync(
        DeviceCodeData data,
        CancellationToken token,
        Dictionary<string, string>? extData = null) =>
        GetClient().AuthorizeWithDeviceAsync(data, token, extData);

    public Task<AuthorizeResult?> AuthorizeWithSilentAsync(
        AuthorizeResult data,
        CancellationToken token,
        Dictionary<string, string>? extData = null) =>
        GetClient().AuthorizeWithSilentAsync(data, token, extData);

    private OpenIdClient GetClient() =>
        _client
        ?? throw new IdentityModelConfigurationException(
            "请先调用 InitializeAsync() 初始化 Yggdrasil Connect 客户端");
}
