using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.IdentityModel.Extensions.Pkce;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Minecraft.IdentityModel.Extensions.OpenId;

public class OpenIdClient(OpenIdOptions options):IOAuthClient
{
    private IOAuthClient? _client;
    /// <summary>
    /// 初始化并从网络加载 OpenId 配置
    /// </summary>
    /// <param name="token"></param>
    /// <param name="checkAddress"></param>
    /// <exception cref="IdentityModelConfigurationException">当要求检查地址并不存在任何授权端点时，将触发此错误</exception>
    public async Task InitializeAsync(CancellationToken token,bool checkAddress = false)
    {
        var opt = await options.BuildOAuthOptionsAsync(token);
        if (checkAddress && opt.Meta.AuthorizeEndpoint.IsNullOrEmpty() && opt.Meta.DeviceEndpoint.IsNullOrEmpty())
            throw new IdentityModelConfigurationException("OpenID 元数据缺少授权代码流端点和设备代码流端点");

        _client = options.EnablePkceSupport ? new PkceClient(opt) : new SimpleOAuthClient(opt);
    }
    /// <summary>
    /// 获取授权代码流地址
    /// </summary>
    /// <param name="scopes">权限列表</param>
    /// <param name="state"></param>
    /// <param name="extData">扩展数据</param>
    /// <returns></returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public string GetAuthorizeUrl(string[] scopes, string state,Dictionary<string,string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 OpenID 客户端");
        return _client.GetAuthorizeUrl(scopes, state, extData);
    }
    /// <summary>
    /// 使用授权代码兑换 Token
    /// </summary>
    /// <param name="code"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(string code, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 OpenID 客户端");
        return await _client.AuthorizeWithCodeAsync(code, token, extData);
    }
    /// <summary>
    /// 获取设备代码流代码对
    /// </summary>
    /// <param name="scopes"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<DeviceCodeData?> GetCodePairAsync(string[] scopes, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 OpenID 客户端");
        return await _client.GetCodePairAsync(scopes, token, extData);
    }
    /// <summary>
    /// 发起一次验证，以检查认证是否成功
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithDeviceAsync(DeviceCodeData data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 OpenID 客户端");
        return await _client.AuthorizeWithDeviceAsync(data, token, extData);
    }
    /// <summary>
    /// 进行一次刷新调用
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="IdentityModelConfigurationException">未调用 <see cref="InitializeAsync"/></exception>
    public async Task<AuthorizeResult?> AuthorizeWithSilentAsync(AuthorizeResult data, CancellationToken token, Dictionary<string, string>? extData = null)
    {
        if (_client is null) throw new IdentityModelConfigurationException("请先调用 InitializeAsync() 初始化 OpenID 客户端");
        return await _client.AuthorizeWithSilentAsync(data, token, extData);
    }
}
