using System;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using PCL.Core.IO.Net.Http.Client.Request;

namespace PCL.Core.Minecraft.IdentityModel.OAuth;

/// <summary>
/// OAuth 客户端实现，配合 Polly 食用效果更佳
/// </summary>
/// <param name="options">OAuth 参数</param>
public sealed class SimpleOAuthClient(OAuthClientOptions options):IOAuthClient
{
    /// <summary>
    /// 获取授权 Url
    /// </summary>
    /// <param name="scopes">访问权限列表</param>
    /// <param name="state"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    public string GetAuthorizeUrl(string[] scopes,string state,Dictionary<string,string>? extData = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Meta.AuthorizeEndpoint);
        var sb = new StringBuilder();
        sb.Append(options.Meta.AuthorizeEndpoint);
        sb.Append($"?response_type=code&scope={Uri.EscapeDataString(string.Join(" ", scopes))}");
        sb.Append($"&redirect_uri={Uri.EscapeDataString(options.RedirectUri)}");
        sb.Append($"&client_id={options.ClientId}&state={state}");
        if (extData is null) return sb.ToString(); 
        foreach (var kvp in extData) 
            sb.Append($"&{kvp.Key}={Uri.EscapeDataString(kvp.Value)}");
        return sb.ToString();
    }
    
    /// <summary>
    /// 使用授权代码获取令牌
    /// </summary>
    /// <param name="code">授权代码</param>
    /// <param name="extData">附加属性，不应该包含必须参数和预定义字段 (e.g. client_id、grant_type)</param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(
        string code,CancellationToken token,Dictionary<string,string>? extData = null
        )
    {
        extData ??= new Dictionary<string, string>();
        extData["client_id"] = options.ClientId;
        extData["grant_type"] = "authorization_code";
        extData["code"] = code;
        var client = options.GetClient.Invoke();
        using var content = new FormUrlEncodedContent(extData);
        using var response = await HttpRequest
            .CreatePost(options.Meta.TokenEndpoint)
            .WithContent(content)
            .WithHeaders(options.Headers ?? [])
            .SendAsync(client, cancellationToken: token)
            .ConfigureAwait(false);
        var result  = await response.Content.ReadAsStringAsync(token);
        return await response
            .AsJsonAsync<AuthorizeResult>(cancellationToken: token)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// 获取设备代码对
    /// </summary>
    /// <param name="scopes"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    public async Task<DeviceCodeData?> GetCodePairAsync
        (string[] scopes,CancellationToken token, Dictionary<string, string>? extData = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.Meta.DeviceEndpoint);
        var client = options.GetClient.Invoke();
        extData ??= new Dictionary<string, string>();
        extData["scope"] = string.Join(" ", scopes);
        extData["client_id"] = options.ClientId;
        var content = new FormUrlEncodedContent(extData);

        using var response = await HttpRequest
            .CreatePost(options.Meta.DeviceEndpoint)
            .WithContent(content)
            .WithHeaders(options.Headers ?? [])
            .SendAsync(client, cancellationToken: token)
            .ConfigureAwait(false);

        return await response
            .AsJsonAsync<DeviceCodeData>(cancellationToken: token)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// 验证用户授权状态 <br/>
    /// 注：此方法不会检查是否过去了 Interval 秒，请自行处理
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    public async Task<AuthorizeResult?> AuthorizeWithDeviceAsync
        (DeviceCodeData data,CancellationToken token,Dictionary<string,string>? extData = null)
    {
        if (data.IsError) throw new OperationCanceledException(data.ErrorDescription); 
        var client = options.GetClient.Invoke();
        extData ??= new Dictionary<string, string>();
        extData["client_id"] = options.ClientId;
        extData["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code";
        extData["device_code"] = data.DeviceCode!;

        using var content = new FormUrlEncodedContent(extData);
        using var response = await HttpRequest
            .CreatePost(options.Meta.TokenEndpoint)
            .WithContent(content)
            .WithHeaders(options.Headers ?? [])
            .SendAsync(client, cancellationToken: token)
            .ConfigureAwait(false);

        return await response
            .AsJsonAsync<AuthorizeResult>(cancellationToken: token)
            .ConfigureAwait(false);
    }
    /// <summary>
    /// 刷新登录
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="extData"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    public async Task<AuthorizeResult?> AuthorizeWithSilentAsync
        (AuthorizeResult data,CancellationToken token,Dictionary<string,string>? extData = null)
    {
        var client = options.GetClient.Invoke();
        if (data.IsError) throw new OperationCanceledException(data.ErrorDescription);
        extData ??= [];
        extData["refresh_token"] = data.RefreshToken!;
        extData["grant_type"] = "refresh_token";
        extData["client_id"] = options.ClientId;
        using var content = new FormUrlEncodedContent(extData);
        using var response = await HttpRequest
            .CreatePost(options.Meta.TokenEndpoint)
            .WithHeaders(options.Headers ?? [])
            .WithContent(content)
            .SendAsync(client, cancellationToken: token)
            .ConfigureAwait(false);

        return await response
            .AsJsonAsync<AuthorizeResult>(cancellationToken: token)
            .ConfigureAwait(false);
    }
}