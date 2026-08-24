using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;
using PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.IdentityModel.Yggdrasil;
using PCL.Core.Minecraft.Profile.Models;
using YggdrasilProfile = PCL.Core.Minecraft.IdentityModel.Yggdrasil.Profile;

namespace PCL.Core.Minecraft.Profile.Authentication;

public sealed class YggdrasilConnectProvider : IAuthenticateProvider
{
    // TODO: 合并前记得删掉或换成 CE 自己的 Client ID !!!
    private static readonly IReadOnlyDictionary<string, string> _BuiltInClientIds =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["littleskin.cn"] = "1014"
        };

    private static readonly string[] _Scopes =
    [
        "openid",
        "offline_access",
        "Yggdrasil.PlayerProfiles.Select",
        "Yggdrasil.Server.Join"
    ];

    private readonly YggdrasilOptions _options;
    private readonly string? _yggdrasilServer;
    private YggdrasilClient? _client;

    public YggdrasilConnectProvider(string discoveryAddress, string? clientId = null, string? yggdrasilServer = null)
    {
        var resolvedClientId = clientId;
        if (string.IsNullOrWhiteSpace(resolvedClientId) &&
            Uri.TryCreate(yggdrasilServer, UriKind.Absolute, out var serverUri) &&
            TryGetBuiltInClientId(serverUri.Host, out var builtInClientId))
            resolvedClientId = builtInClientId;

        _options = new YggdrasilOptions
        {
            OpenIdDiscoveryAddress = discoveryAddress,
            ClientId = resolvedClientId?.Trim() ?? string.Empty,
            OnlyDeviceAuthorize = true,
            GetClient = () => NetworkService.GetClient(NetworkService.Default)
        };
        _yggdrasilServer = yggdrasilServer;
    }

    public static bool TryGetBuiltInClientId(string host, out string clientId)
    {
        var normalizedHost = host.Trim().TrimEnd('.');
        return _BuiltInClientIds.TryGetValue(normalizedHost, out clientId!);
    }

    public async Task InitializeAsync(CancellationToken token)
    {
        _client = new YggdrasilClient(_options);
        await _client.InitializeAsync(token).ConfigureAwait(false);
    }

    public Task<DeviceCodeData?> GetCodePairAsync(CancellationToken token)
    {
        EnsureInitialized();
        return _client!.GetCodePairAsync(_Scopes, token);
    }

    public Task<AuthorizeResult?> PollDeviceCodeAsync(DeviceCodeData data, CancellationToken token)
    {
        EnsureInitialized();
        return _client!.AuthorizeWithDeviceAsync(data, token);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken token)
    {
        EnsureInitialized();
        var oauth = request.OAuthResult;
        if (oauth is null && !string.IsNullOrWhiteSpace(request.RefreshToken))
            oauth = await _client!.AuthorizeWithSilentAsync(new AuthorizeResult { RefreshToken = request.RefreshToken }, token)
                .ConfigureAwait(false);
        if (oauth is null && request.DeviceCodeHandler is not null)
        {
            var code = await GetCodePairAsync(token).ConfigureAwait(false)
                       ?? throw new IdentityModelAuthenticationException("device_authorization_failed", "The server returned no device code.");
            oauth = await request.DeviceCodeHandler(
                new DeviceCodeAuthenticationContext(code, pollToken => PollDeviceCodeAsync(code, pollToken)), token)
                .ConfigureAwait(false);
        }
        if (oauth is null)
            throw new IdentityModelConfigurationException("Yggdrasil Connect login requires an OAuth result or device-code handler.");
        return await CompleteAsync(oauth, request, token).ConfigureAwait(false);
    }

    public Task<AuthenticationResult> RefreshAsync(McProfile profile, CancellationToken token)
        => AuthenticateAsync(new AuthenticationRequest
        {
            RefreshToken = profile.RefreshToken,
            IdToken = profile.IdToken
        }, token);

    public async Task<bool> ValidateAsync(McProfile profile, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(profile.AccessToken)) return false;
        EnsureInitialized();
        return await new AuthlibProvider(_GetServerAddress()).ValidateAsync(profile, token).ConfigureAwait(false);
    }

    private async Task<AuthenticationResult> CompleteAsync(AuthorizeResult oauth, AuthenticationRequest request, CancellationToken token)
    {
        if (oauth.IsError) throw new IdentityModelAuthenticationException(oauth.Error, oauth.ErrorDescription);
        if (string.IsNullOrWhiteSpace(oauth.AccessToken))
            throw new IdentityModelAuthenticationException("invalid_token", "Yggdrasil Connect returned no access token.");

        var claims = string.IsNullOrWhiteSpace(oauth.IdToken)
            ? null
            : await _ReadIdTokenAsync(oauth.IdToken, token).ConfigureAwait(false);
        var profile = claims?.SelectedProfile;
        var available = claims?.AvailableProfiles ?? [];
        if (profile is null || available.Length == 0)
        {
            var userInfo = await _GetUserInfoAsync(oauth.AccessToken, token).ConfigureAwait(false);
            profile ??= userInfo?.SelectedProfile;
            if (available.Length == 0) available = userInfo?.AvailableProfiles ?? [];
        }
        if ((request.ForceReselectProfile || profile is null) && available.Length > 1)
        {
            if (request.ProfileSelector is null)
                throw new IdentityModelAuthenticationException("profile_selection_required", "Yggdrasil Connect returned multiple player profiles.");
            var candidates = available.Select(p => new AuthenticationCandidate(p.Id, p.Name ?? p.Id)).ToArray();
            var selected = await request.ProfileSelector(candidates, token).ConfigureAwait(false);
            profile = selected is null ? null : available.FirstOrDefault(p => p.Id == selected.Id);
        }
        profile ??= available.FirstOrDefault();
        if (profile is null)
            throw new IdentityModelAuthenticationException("invalid_profile", "Yggdrasil Connect returned no player profile.");

        return new AuthenticationResult
        {
            ProfileType = ProfileType.YggdrasilConnect,
            UserName = profile.Name ?? string.Empty,
            Uuid = profile.Id,
            AccessToken = oauth.AccessToken,
            RefreshToken = oauth.RefreshToken ?? request.RefreshToken ?? string.Empty,
            ClientToken = profile.Id,
            TokenType = "Bearer",
            ExpiresAt = oauth.ExpiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(oauth.ExpiresIn.Value) : null,
            Provider = "yggdrasil-connect",
            Server = _GetServerAddress(),
            ServerName = await _GetServerNameAsync(token).ConfigureAwait(false),
            DiscoveryAddress = _options.OpenIdDiscoveryAddress,
            IdToken = oauth.IdToken ?? request.IdToken
        };
    }

    private async Task<YggdrasilConnectClaims> _ReadIdTokenAsync(string? idToken, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            throw new SecurityException("Yggdrasil Connect did not return an ID token.");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
        if (string.IsNullOrWhiteSpace(jwt.Header.Kid))
            throw new SecurityException("Yggdrasil Connect ID token has no key id.");
        var key = await _options.GetSignatureKeyAsync(jwt.Header.Kid, token).ConfigureAwait(false);
        var verifiedToken = new JsonWebToken(idToken, _options.Meta!);
        var verified = verifiedToken.VerifySignature(key, _options.ClientId);
        if (verified is null) throw new SecurityException("Yggdrasil Connect ID token validation failed.");
        return verifiedToken.ReadTokenPayload<YggdrasilConnectClaims>()
               ?? throw new SecurityException("Yggdrasil Connect ID token payload was empty.");
    }

    private async Task<YggdrasilConnectClaims?> _GetUserInfoAsync(string accessToken, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_options.Meta?.UserInfoEndpoint)) return null;
        using var response = await HttpRequest.Create(_options.Meta!.UserInfoEndpoint)
            .WithBearerToken(accessToken)
            .SendAsync(NetworkService.GetClient(NetworkService.Default), cancellationToken: token)
            .ConfigureAwait(false);
        if (!response.IsSuccess) return null;
        return await response.AsJsonAsync<YggdrasilConnectClaims>(cancellationToken: token).ConfigureAwait(false);
    }

    private async Task<string?> _GetServerNameAsync(CancellationToken token)
    {
        try
        {
            var root = _GetServerAddress().TrimEnd('/');
            if (root.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase))
                root = root[..^"/authserver".Length];
            using var response = await HttpRequest.Create(root)
                .SendAsync(NetworkService.GetClient(NetworkService.Default), cancellationToken: token)
                .ConfigureAwait(false);
            if (!response.IsSuccess) return null;
            var metadata = await response.AsJsonAsync<JsonObject>(cancellationToken: token).ConfigureAwait(false);
            return metadata?["meta"]?["serverName"]?.ToString();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private void EnsureInitialized()
    {
        if (_client is null || _options.Meta is null)
            throw new IdentityModelConfigurationException("Please initialize Yggdrasil Connect before use.");
    }

    private string _GetServerAddress()
    {
        if (!string.IsNullOrWhiteSpace(_yggdrasilServer))
        {
            var server = _yggdrasilServer.TrimEnd('/');
            return server.EndsWith("/authserver", StringComparison.OrdinalIgnoreCase) ? server : server + "/authserver";
        }
        var value = _options.OpenIdDiscoveryAddress;
        var marker = value.IndexOf("/.well-known/", StringComparison.OrdinalIgnoreCase);
        return marker > 0 ? value[..marker].TrimEnd('/') : value.TrimEnd('/');
    }

    private sealed record YggdrasilConnectClaims
    {
        [JsonPropertyName("sub")] public string? Subject { get; init; }
        [JsonPropertyName("aud")] public string? Audience { get; init; }
        [JsonPropertyName("selectedProfile")] public YggdrasilProfile? SelectedProfile { get; init; }
        [JsonPropertyName("availableProfiles")] public YggdrasilProfile[]? AvailableProfiles { get; init; }
    }
}
