using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

public record OpenIdOptions(Func<HttpClient> GetHttpClient, string ConfigurationAddress)
{
    public string OpenIdDiscoveryAddress => ConfigurationAddress;
    public required string ClientId
    {
        get;
        set;
    }
    
    public bool OnlyDeviceAuthorize { get; set; }

    public string? RedirectUri;

    public Dictionary<string, string>? Headers { get; set; }

    public bool EnablePkceSupport { get; set; } = true;
    public Func<HttpClient> GetClient => GetHttpClient;
    public OpenIdMetadata? Meta { get; set; }
    


    public virtual async Task InitiateAsync(CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, OpenIdDiscoveryAddress);
        if (Headers is not null)
            foreach (var kvp in Headers)
            {
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }

        var requestTask = GetClient.Invoke().SendAsync(request, token);
        using var response = await requestTask;
        var task =  response.Content.ReadAsStringAsync(token);
        Meta = JsonSerializer.Deserialize<OpenIdMetadata>(await task);
    }

    public async Task<JsonWebKey> GetSignatureKeyAsync(string kid,CancellationToken token)
    {
        if (Meta?.JwksUri is null) throw new InvalidOperationException();
        using var request = new HttpRequestMessage(HttpMethod.Get, Meta.JwksUri);
        if (Headers is not null)
            foreach (var kvp in Headers)
            {
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }
        using var response = await GetClient.Invoke().SendAsync(request, token);
        var result = JsonSerializer.Deserialize<JsonWebKeys>(await response.Content.ReadAsStringAsync(token));
        return result?.Keys.Single(k => k.Kid == kid) 
               ?? throw new FormatException();
    }

    public virtual async Task<OAuthClientOptions> BuildOAuthOptionsAsync(CancellationToken token)
    {
        await InitiateAsync(token);
        if(!OnlyDeviceAuthorize) ArgumentException.ThrowIfNullOrEmpty(RedirectUri);
        return new OAuthClientOptions
        {
            GetClient = GetClient,
            ClientId = ClientId,
            RedirectUri = OnlyDeviceAuthorize ? string.Empty:RedirectUri!,
            Meta = new EndpointMeta
            {
                AuthorizeEndpoint = Meta?.AuthorizationEndpoint??string.Empty,
                DeviceEndpoint = Meta?.DeviceAuthorizationEndpoint??string.Empty,
                TokenEndpoint = Meta!.TokenEndpoint,
            }
        };
    }
}