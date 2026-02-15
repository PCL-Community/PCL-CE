using System;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;


namespace PCL.Core.IdentityModel.OAuth;

/// <summary>
/// OAuth 客户端实现
/// </summary>
/// <param name="getClient">获取 HttpClient 的方法</param>
/// <param name="options">OAuth 参数</param>
public sealed class SimpleOAuthClient(Func<HttpClient> getClient, OAuthClientOptions options)
{
    /// <summary>
    /// 获取授权 Url
    /// </summary>
    /// <param name="scopes">访问权限列表</param>
    /// <param name="redirectUri">重定向 Url</param>
    /// <returns></returns>
    public string GetAuthorizeUrl(string[] scopes,string redirectUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Meta.AuthorizeEndpoint);
        var sb = new StringBuilder();
        sb.Append(options.Meta.AuthorizeEndpoint);
        sb.Append($"?response_type=code&scope={string.Join(" ", scopes)}");
        sb.Append($"&redirect_uri={redirectUri}&client_id={options.Meta.ClientId}");
        return Uri.EscapeDataString(sb.ToString());
    }
    
    /// <summary>
    /// 使用授权代码获取 AccessToken
    /// </summary>
    /// <param name="code">授权代码</param>
    /// <param name="extData">附加属性，不应该包含必须参数和预定义字段 (e.g. client_id、grant_type)</param>
    /// <returns></returns>
    public async Task<AuthorizeResult?> AuthorizeWithCodeAsync(
        string code,Dictionary<string,string>? extData = null
        )
    {
        extData ??= new Dictionary<string, string>();
        extData["client_id"] = options.Meta.ClientId;
        extData["grant_type"] = "authorization_code";
        extData["code"] = code;
        var client = getClient.Invoke();
        using var content = new FormUrlEncodedContent(extData);
        using var request = new HttpRequestMessage(HttpMethod.Post,options.Meta.TokenEndpoint);
        request.Content = content;
        if(options.Headers is not null)
            foreach (var kvp in options.Headers)
                _ = request.Headers.TryAddWithoutValidation(kvp.Key,kvp.Value);
        using var response = await client.SendAsync(request);
        var result  = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<AuthorizeResult>(result);
    }

    public async Task<DeviceCodeData?> GetCodePairAsync
        (string[] scopes, Dictionary<string, string>? extData = null)
    {
        var client = getClient.Invoke();
        extData ??= new Dictionary<string, string>();
        extData["scope"] = string.Join(" ", scopes);
        extData["client_id"] = options.Meta.ClientId;
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Meta.DeviceEndpoint);
        var content = new FormUrlEncodedContent(extData);
        request.Content = content;
        if(options.Headers is not null)
            foreach (var kvp in options.Headers)
                _ = request.Headers.TryAddWithoutValidation(kvp.Key,kvp.Value);
        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<DeviceCodeData>(result);
    }

    public async Task<AuthorizeResult> AuthorizeWithDeviceCode(DeviceCodeData data,Dictionary<string,string> extData)
    {
        var client = getClient.Invoke();
    }

    public async Task<AuthorizeResult> AuthorizeWithSilentAsync(AuthorizeResult data)
    {
        
    }
}