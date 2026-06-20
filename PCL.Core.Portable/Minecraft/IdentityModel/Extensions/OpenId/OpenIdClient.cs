// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.Minecraft.IdentityModel.Extensions.Pkce;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

/// <summary>
/// Cross-platform OpenID Connect client backed by the portable OAuth clients.
/// </summary>
public class OpenIdClient(OpenIdOptions options) : IOAuthClient
{
    private IOAuthClient? _client;

    public async Task InitializeAsync(
        CancellationToken token,
        bool checkAddress = false)
    {
        if (options.Meta is null)
            await options.InitializeAsync(token).ConfigureAwait(false);

        var oauthOptions = await options
            .BuildOAuthOptionsAsync(token)
            .ConfigureAwait(false);
        if (checkAddress &&
            string.IsNullOrWhiteSpace(oauthOptions.Meta.AuthorizeEndpoint) &&
            string.IsNullOrWhiteSpace(oauthOptions.Meta.DeviceEndpoint))
            throw new IdentityModelConfigurationException(
                "OpenID 元数据缺少授权代码流端点和设备代码流端点");

        _client = options.EnablePkceSupport
            ? new PkceClient(oauthOptions)
            : new SimpleOAuthClient(oauthOptions);
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

    private IOAuthClient GetClient() =>
        _client
        ?? throw new IdentityModelConfigurationException(
            "请先调用 InitializeAsync() 初始化 OpenID 客户端");
}
