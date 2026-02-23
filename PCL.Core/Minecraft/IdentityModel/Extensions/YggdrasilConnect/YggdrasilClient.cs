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
    /// 初始化并拉取网络配置
    /// </summary>
    /// <exception cref="ArgumentException">当无法获取 ClientId 时抛出，调用方应该设置 ClientId 并重新实例化 Client</exception>
    /// <param name="token"></param>
    public async Task InitializeAsync(CancellationToken token)
    {
        _client = new OpenIdClient(_options);
        await _client.InitializeAsync(token,true);
    }
    /// <summary>
    /// 获取授权端点地址
    /// </summary>
    /// <param name="scopes"></param>
    /// <param name="state"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitializeAsync"/></exception>
    public string GetAuthorizeUrl(string[] scopes, string state, Dictionary<string, string>? extData)
    {
        if (_client is null) throw new InvalidOperationException();
        return _client.GetAuthorizeUrl(scopes, state, extData);
    }
    /// <summary>
    /// 使用授权代码兑换令牌
    /// </summary>
    /// <param name="code"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.AuthorizeWithCodeAsync(code, token, extData);

    }
    /// <summary>
    /// 获取代码对
    /// </summary>
    /// <param name="scopes"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.GetCodePairAsync(scopes, token, extData);
        
    }
    /// <summary>
    /// 发起一次请求验证用户授权状态
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.AuthorizeWithDeviceAsync(data, token, extData);

    }
    /// <summary>
    /// 刷新登录
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new InvalidOperationException();
        return await _client.AuthorizeWithSilentAsync(data, token, extData);
    }
}