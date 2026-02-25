using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Minecraft.IdentityModel.Yggdrasil;

/// <summary>
/// 提供 Yggdrasil 传统认证支持
/// </summary>
/// <param name="options"><see cref="YggdrasilLegacyAuthenticateOptions"/> 认证参数</param>
public sealed class YggdrasilLegacyClient(YggdrasilLegacyAuthenticateOptions options)
{
    /// <summary>
    /// 异步向服务器发送一次登录请求
    /// </summary>
    /// <param name="token"></param>
    /// <returns><see cref="YggdrasilAuthenticateResult"/>  认证结果</returns>
    /// <exception cref="ArgumentException">用户名或密码无效</exception>
    public async Task<YggdrasilAuthenticateResult?> AuthenticateAsync(CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.Username);
        ArgumentException.ThrowIfNullOrEmpty(options.Password);
        
        var credential = new YggdrasilCredential
        {
            User = options.Username,
            Password = options.Password,
        };
        var address = $"{options.YggdrasilApiLocation}/authserver/authenticate";
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        if(options.Headers is not null)
            foreach (var kvp in options.Headers)
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        
        using var content = 
            new StringContent(JsonSerializer.Serialize(credential), Encoding.UTF8, "application/json");
        request.Content = content;
        using var response = await options.GetClient.Invoke().SendAsync(request,token);
        return 
            JsonSerializer.Deserialize<YggdrasilAuthenticateResult>(await response.Content.ReadAsStringAsync(token));
        
    }
    /// <summary>
    /// 异步向服务器发送一次刷新请求
    /// </summary>
    /// <param name="token"></param>
    /// <param name="seleectedProfile">如果需要选择角色，请填写此参数</param>
    public async Task<YggdrasilAuthenticateResult?> RefreshAsync(CancellationToken token,Profile? seleectedProfile)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.AccessToken);
        
        var refreshData = new YggdrasilRefresh()
        {
            AccessToken = options.AccessToken
        };
        if (seleectedProfile is not null) refreshData.SelectedProfile = seleectedProfile;
        
        var address = $"{options.YggdrasilApiLocation}/authserver/refresh";
        
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        if(options.Headers is not null)
            foreach (var kvp in options.Headers)
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        using var content = new StringContent(
            JsonSerializer.Serialize(refreshData), Encoding.UTF8, "application/json");
        request.Content = content;
        using var response = await options.GetClient.Invoke().SendAsync(request, token);
        return JsonSerializer.Deserialize<YggdrasilAuthenticateResult>(await response.Content.ReadAsStringAsync(token));
    }
    /// <summary>
    /// 异步向服务器发送一次验证请求
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<bool> ValidateAsync(CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.AccessToken);

        var validateData = new YggdrasilRefresh()
        {
            AccessToken = options.AccessToken
        };
        var address = $"{options.YggdrasilApiLocation}/authserver/invalidate";
        
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        if(options.Headers is not null)
            foreach (var kvp in options.Headers)
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        
        using var content = new StringContent(
            JsonSerializer.Serialize(validateData), Encoding.UTF8, "application/json");
        request.Content = content;
        using var response = await options.GetClient.Invoke().SendAsync(request, token);
        return response.StatusCode == HttpStatusCode.NoContent;
    }
    
    /// <summary>
    /// 异步向服务器发送一次注销请求
    /// </summary>
    /// <param name="token"></param>
    public async Task InvalidateAsync(CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.AccessToken);

        var validateData = new YggdrasilRefresh()
        {
            AccessToken = options.AccessToken
        };
        var address = $"{options.YggdrasilApiLocation}/authserver/invalidate";
        
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        if(options.Headers is not null)
            foreach (var kvp in options.Headers)
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        
        using var content = new StringContent(
            JsonSerializer.Serialize(validateData), Encoding.UTF8, "application/json");
        request.Content = content;
        await options.GetClient.Invoke().SendAsync(request, token);
    }
    /// <summary>
    /// 异步向服务器发送登出请求 <br/>
    /// 这会立刻注销所有会话，无论当前会话是否属于调用方
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<(bool IsSuccess,string ErrorDescription)> SignOutAsync(CancellationToken token)
    {
        // 不想写 Model 了，就这样吧（趴
        var signoutData = new JsonObject
        {
            ["username"] = options.Username,
            ["password"] = options.Password
        }.ToJsonString();
        var address = $"{options.YggdrasilApiLocation}/authserver/signout";
        
        using var request = new HttpRequestMessage(HttpMethod.Post, address);
        if(options.Headers is not null)
            foreach (var kvp in options.Headers)
                _ = request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
        using var content = new StringContent(signoutData, Encoding.UTF8, "application/json");
        request.Content = content;
        using var response = await options.GetClient.Invoke().SendAsync(request, token);
        var data = JsonNode.Parse(await response.Content.ReadAsStringAsync(token));
        return (response.StatusCode == HttpStatusCode.NoContent, data?["errorMessage"]?.ToString()!);
    }
}