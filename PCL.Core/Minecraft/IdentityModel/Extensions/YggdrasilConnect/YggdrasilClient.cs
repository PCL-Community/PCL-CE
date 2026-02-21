using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

// Steven Qiu 说这东西完全就是 OpenId + 魔改了一部分，所以可以直接复用 OpenId 的逻辑

/// <summary>
/// 
/// </summary>
public class YggdrasilClient:IOAuthClient
{

    private OpenIdClient? _client;

    private YggdrasilOptions _options;
    
    public YggdrasilClient(YggdrasilOptions options)
    {
        _options = options;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <exception cref="ArgumentException">当无法获取 ClientId 时抛出，调用方应该设置 ClientId 并重新实例化 OpenId Client</exception>
    /// <param name="token"></param>
    public async Task InitialAsync(CancellationToken token)
    {
        _client = new OpenIdClient(_options);
        await _client.InitialAsync(token);
    }
    /// <summary>
    /// 获取授权端点地址
    /// </summary>
    /// <param name="scopes"></param>
    /// <param name="redirectUri"></param>
    /// <param name="state"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitialAsync"/></exception>
    public string GetAuthorizeUrl(string[] scopes, string redirectUri, string state, Dictionary<string, string>? extData)
    {
        if (_client is null) throw new InvalidOperationException();
        return _client.GetAuthorizeUrl(scopes, redirectUri, state, extData);
    }

    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.AuthorizeWithCodeAsync(code, token, extData);

    }

    public async Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.GetCodePairAsync(scopes, token, extData);
        
    }

    public async Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.AuthorizeWithDeviceAsync(data, token, extData);

    }

    public async Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.AuthorizeWithSilentAsync(data, token, extData);
    }
}