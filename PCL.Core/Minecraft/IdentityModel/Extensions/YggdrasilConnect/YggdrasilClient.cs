using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.YggdrasilConnect;

// Steven Qiu 说这东西完全就是 OpenId + 魔改了一部分，所以可以直接复用 OpenId 的逻辑

/// <summary>
/// Yggdrasil Connect Client 客户端
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
    /// <exception cref="IdentityModelConfigurationException">无法获取 ClientId 或 OpenID 元数据无效</exception>
    /// <param name="token"></param>
    public async Task InitializeAsync(CancellationToken token)
    {
        _client = new OpenIdClient(_options);
        await _client.InitializeAsync(token, true);
    }
    
    /// <summary>
    /// 获取授权端点地址
    /// </summary>
    /// <param name="scopes">Yggdrasil Connect 规范所规定的权限</param>
    /// <param name="state">用于关联会话</param>
    /// <param name="extData">扩展数据 <br/> NOTE: 由 RFC 6749 预先定义的字段将会被覆盖，请避免填写</param>
    /// <returns>OAuth 授权地址</returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public string GetAuthorizeUrl(string[] scopes, string state, Dictionary<string, string>? extData)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 Yggdrasil Connect 客户端");
        return _client.GetAuthorizeUrl(scopes, state, extData);
    }
    
    /// <summary>
    /// 使用授权代码兑换令牌
    /// </summary>
    /// <param name="code">授权代码</param>
    /// <param name="token">取消令牌</param>
    /// <param name="extData">扩展数据 <br/> NOTE: 由 RFC 6749 预先定义的字段将会被覆盖，请避免填写</param>
    /// <returns><see cref="AuthorizeResult"> 授权结果</returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 Yggdrasil Connect 客户端");
        return await _client.AuthorizeWithCodeAsync(code, token, extData);

    }
    
    /// <summary>
    /// 获取代码对
    /// </summary>
    /// <param name="scopes">Yggdrasil Connect 规范所规定的权限</param>
    /// <param name="token">取消令牌</param>
    /// <param name="extData">扩展数据 <br/> NOTE: 由 RFC 6749 预先定义的字段将会被覆盖，请避免填写</param>
    /// <returns><see cref="DeviceCodeData"> 设备流数据</returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 Yggdrasil Connect 客户端");
        return await _client.GetCodePairAsync(scopes, token, extData);

    }
    
    /// <summary>
    /// 发起一次请求验证用户授权状态
    /// </summary>
    /// <param name="data"><see cref="DeviceCodeData"> 设备流数据</param>
    /// <param name="token">取消令牌</param>
    /// <param name="extData">扩展数据 <br/> NOTE: 由 RFC 6749 预先定义的字段将会被覆盖，请避免填写</param>
    /// <returns><see cref="AuthorizeResult"> 授权结果</returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 Yggdrasil Connect 客户端");
        return await _client.AuthorizeWithDeviceAsync(data, token, extData);

    }
    
    /// <summary>
    /// 刷新登录
    /// </summary>    
    /// <param name="data"><see cref="AuthorizeResult"> 先前的授权结果</param>
    /// <param name="token">取消令牌</param>
    /// <param name="extData">扩展数据 <br/> NOTE: 由 RFC 6749 预先定义的字段将会被覆盖，请避免填写</param>
    /// <returns><see cref="AuthorizeResult"> 授权结果</returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 Yggdrasil Connect 客户端");
        return await _client.AuthorizeWithSilentAsync(data, token, extData);
    }
}
