using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using PCL.Core.IO.Net.Http.Client.Request;
using PCL.Core.Minecraft.IdentityModel.Extensions.JsonWebToken;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

public record OpenIdOptions
{
    /// <summary>
    /// OpenId Discovery 地址
    /// </summary>
    public required string OpenIdDiscoveryAddress { get; set; }
    /// <summary>
    /// 客户端 ID（必须设置）
    /// </summary>
    public required string ClientId
    {
        get;
        set;
    }
    
    // 为了让 YggdrasilConnect Client 复用代码做的逻辑
    
    /// <summary>
    /// 是否只使用设备代码流授权
    /// </summary>
    public bool OnlyDeviceAuthorize { get; set; }
    /// <summary>
    /// 回调 Uri
    /// </summary>
    public string? RedirectUri { get; set; }
    /// <summary>
    /// 发送 HTTP 请求时设置的请求头，仅适用于请求头（丢到 HttpRequestMessage 不会报错的那种）
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }
    /// <summary>
    /// 是否启用 PKCE 支持，默认启用
    /// </summary>
    public bool EnablePkceSupport { get; set; } = true;
    /// <summary>
    /// 获取 HttpClient，生命周期由调用方管理
    /// </summary>
    public required Func<HttpClient> GetClient { get; set; }
    /// <summary>
    /// OpenId 元数据，请勿自行设置此属性，而是应该调用 <see cref="InitializeAsync"/>
    /// </summary>
    public OpenIdMetadata? Meta { get; internal set; }
    
    /// <summary>
    /// 从互联网拉取 OpenID 配置信息
    /// </summary>
    /// <param name="token"></param>
    public virtual async Task InitializeAsync(CancellationToken token)
    {
        using var response = await HttpRequest
            .Create(OpenIdDiscoveryAddress)
            .WithHeaders(Headers ?? [])
            .SendAsync(GetClient.Invoke(), cancellationToken: token)
            .ConfigureAwait(false);

        Meta = await response
            .AsJsonAsync<OpenIdMetadata>(cancellationToken: token)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// 获取 Json Web Key
    /// </summary>
    /// <param name="kid">密钥 ID</param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitializeAsync"/></exception>
    /// <exception cref="FormatException">找不到 Jwk 或 Jwk 配置无效</exception>
    public async Task<JsonWebKey> GetSignatureKeyAsync(string kid,CancellationToken token)
    {
        if (Meta?.JwksUri is null) throw new InvalidOperationException();
        using var response = await HttpRequest.Create(Meta.JwksUri)
            .WithHeaders(Headers ?? [])
            .SendAsync(GetClient.Invoke())
            .ConfigureAwait(false);

        var result = await response
            .AsJsonAsync<JsonWebKeys>(cancellationToken: token)
            .ConfigureAwait(false);
        return result?.Keys.Single(k => k.Kid == kid) 
               ?? throw new FormatException();
    }
    /// <summary>
    /// 构建 OAuth 客户端配置
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitializeAsync"/></exception>
    public virtual async Task<OAuthClientOptions> BuildOAuthOptionsAsync(CancellationToken token)
    {
        if (Meta is null) throw new InvalidOperationException();
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
