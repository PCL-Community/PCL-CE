// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

public record YggdrasilOptions : OpenIdOptions
{
    private static readonly string[] RequiredScopes =
    [
        "openid",
        "Yggdrasil.PlayerProfiles.Select",
        "Yggdrasil.Server.Join"
    ];

    public override async Task InitializeAsync(CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(OpenIdDiscoveryAddress);
        var metadata = await GetJsonAsync(
                OpenIdDiscoveryAddress,
                YggdrasilJsonContext.Default.YggdrasilConnectMetaData,
                token)
            .ConfigureAwait(false);
        Meta = metadata;

        List<string>? missingScopes = null;
        foreach (var requiredScope in RequiredScopes)
        {
            if (metadata.ScopesSupported.Contains(requiredScope, StringComparer.Ordinal))
                continue;
            missingScopes ??= [];
            missingScopes.Add(requiredScope);
        }

        if (missingScopes is not null)
            throw new IdentityModelConfigurationException(
                $"Yggdrasil Connect 元数据缺少必要 scope：{string.Join(", ", missingScopes)}");
    }

    public override async Task<OAuthClientOptions> BuildOAuthOptionsAsync(
        CancellationToken token)
    {
        if (Meta is not YggdrasilConnectMetaData metadata)
            throw new IdentityModelConfigurationException(
                "请先调用 InitializeAsync() 加载 Yggdrasil Connect 元数据");

        var options = await base
            .BuildOAuthOptionsAsync(token)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(options.ClientId))
            return options;
        if (!string.IsNullOrWhiteSpace(metadata.SharedClientId))
        {
            options.ClientId = metadata.SharedClientId;
            return options;
        }

        throw new IdentityModelConfigurationException(
            "Yggdrasil Connect 需要设置 ClientId，或由元数据提供 sharedClientId");
    }
}
