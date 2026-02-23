using System;
using PCL.Core.Utils.Exts;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils.Hash;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.Pkce;

/// <summary>
/// 带 PKCE 支持的客户端 <br/>
/// 此客户端并非线程安全，请勿在多个线程间共享示例
/// </summary>
/// <param name="options"></param>
public class PkceClient(OAuthClientOptions options):IOAuthClient
{
    private byte[] _ChallengeCode { get; set; } = new byte[32];
    private bool _isCallGetAuthorizeUrl;
    /// <summary>
    /// 设置验证方法，支持 PlainText 和 SHA256
    /// </summary>
    public PkceChallengeOptions ChallengeMethod { get; private set; } = PkceChallengeOptions.Sha256;
    private readonly SimpleOAuthClient _client = new(options);
    /// <summary>
    /// 获取授权地址
    /// </summary>
    /// <param name="scopes"></param>
    /// <param name="state"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    public string GetAuthorizeUrl(string[] scopes, string state, Dictionary<string, string>? extData)
    {
        RandomNumberGenerator.Fill(_ChallengeCode);
        extData ??= [];
        extData["code_challenge"] = ChallengeMethod == PkceChallengeOptions.Sha256
            ? SHA256Provider.Instance.ComputeHash(_ChallengeCode).ToHexString()
            : _ChallengeCode.FromBytesToB64UrlSafe();
        extData["code_challenge_method"] = ChallengeMethod == PkceChallengeOptions.Sha256 ? "S256":"plain";
        _isCallGetAuthorizeUrl = true;
        return _client.GetAuthorizeUrl(scopes, state, extData);
    }
    /// <summary>
    /// 使用授权代码兑换令牌
    /// </summary>
    /// <param name="code"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (!_isCallGetAuthorizeUrl) throw new InvalidOperationException("Challenge code is invalid");
        var pkce = _ChallengeCode.FromBytesToB64UrlSafe();
        extData ??= [];
        extData["code_verifier"] = pkce;
        _isCallGetAuthorizeUrl = false;
        return await _client.AuthorizeWithCodeAsync(code, token, extData);
    }
    /// <summary>
    /// 获取代码对
    /// </summary>
    /// <param name="scopes"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    public async Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        return await _client.GetCodePairAsync(scopes, token, extData);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    public async Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        return await _client.AuthorizeWithDeviceAsync(data, token, extData);
    }

    public async Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        return await _client.AuthorizeWithSilentAsync(data, token, extData);
    }
}

/*
 *
 *本 PR 旨在提供规范的身份认证组件，以解决 PCL 内部的身份认证实现不规范还一堆 Bug 完全没有可维护性的问题

## ToDo List

- [x] 实现 OAuth 认证（RFC 6749/RFC 8628）
- [x] 支持 OAuth 扩展实现（PKCE）
- [x] 支持 OpenId Connect（精简版）
- [x] 支持 Yggdrasil Connect
- [x] 支持传统 Yggdrasil

# 用例

>[!IMPORTANT]
> ## IdentityModel 不会尝试处理任何错误
> 
>
> 因为其设计目标是作为协议传输层将调用方提供的数据转换为标准数据格式（类似 HttpClient）
> 错误需要调用方自行处理

## 普通 OAuth 

### 初始化

```csharp
var option = new OAuthClientOptions()
{
            ClientId = "0712",
            GetClient = () => _client,
            Headers = new(){
                ["User-Agent"] = "PCL-CE/2.14.2"
            },
            Meta = new EndpointMeta
            {
                AuthorizeEndpoint = "https://open.example.com/oauth/v2.0/authorize",
                DeviceEndpoint = "https://open.example.com/oauth/v2.0/device",
                TokenEndpoint = "https://open.example.com/oauth/v2.0/token"
            },
            RedirectUri = "http://localhost:7120/oauth/callback"
};

var client = new SimpleOAuthClient(option);
```

>[!Tip]
>
> ### PKCE 扩展
>
> 如果需要 PKCE 扩展支持，，请使用 PkceClient 而不是 SimpleOAuthClient

### 授权代码流

获取授权 Url

```csharp
var authorizeUri = client.GetAuthorizeUrl(["offline_access"],"20120712");
```
使用授权代码兑换令牌

```csharp
var result = await client.AuthorizeWithCodeAsync("",CancellationToken.None);
```

### 设备代码流

获取代码对

```csharp
var data = await client.GetCodePairAsync(["offline_access"], CancellationToken.None);
```

```csharp
var data = await client.AuthorizeWithDeviceAsync(data, CancellationToken.None);
```

>[!IMPORTANT] 
> ### AuthorizeWithDeviceAsync 仅会发送一次请求（不会轮询） 
>
> 你可以配合 Polly 做重试，或者自己糊也行，但绝对不能只调用一次

### 刷新登录 

```csharp
await client.AuthorizeWithSilentAsync(data, CancellationToken.None);
```

>[!TIP]
> ### 扩展数据支持 
>
> 如果某一个协议基于 OAuth 但需要提供更多的请求载荷，你可以设置每个方法的 extData 参数（字典）并提供对应的数据

>[!WARNING]
>
> ### 不要填写预定义字段
> 
> 请不要试图填写注入 `client_id` `grant_type` 之类的由 RFC 预先定义的字段，这些字段会被覆盖掉
>
> 如果实在有需要，请重新开一个类并实现 IOAuthClient 接口

## OpenId Connect 

>[!IMPORTANT]
>
> IdentityModel 提供的实现为精简版，可能不是标准 OpenID 实现，但应该够用.....吧？

### 初始化

>[!TIP]
> ### 设备代码流模式
>
> 如果只需要设备代码流登录，请设置 OnlyDeviceAuthorize 为 true，这将跳过 RedirectUri 的检查，从而允许传入空值

```
var options = new OpenIdOptions{
            OpenIdDiscoveryAddress = "https://openid.example.com/.well-known/openid-configuration",
            ClientId = "0712",
            GetClient = () => _client
};
var client = new OpenIdClient(options);

client.InitializeAsync(CancellationToken.None)
```

>[!IMPORTANT]
> 因为需要从互联网拉取配置，基于 OpenID 协议（包括 OpenID）实现的客户端均需要在开始使用前调用 `.InitializeAsync()`

>[!TIP]
>
> ### PKCE 支持
>
> OpenID Client 原生支持（并默认启用） PKCE 扩展
> 
> 如果 PKCE 扩展支持导致登录问题，请设置 EnablePkceSupport 为 false

OpenID Client 的登录过程与 OAuth 相同，请参考 SimpleOAuthClient 的用例

## Yggdrasil Connect 

Yggdrasil Connect Client 的初始化方式与 OpenID Client 相同，请直接参考 OpenID Client 的初始化方式

## Yggdrasli Legacy Login

### 初始化

```
```
 * 
 */