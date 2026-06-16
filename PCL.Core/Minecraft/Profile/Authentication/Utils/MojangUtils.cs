using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.Profile.Authentication.Utils.Models;
using PCL.Core.Minecraft.Profile.Models;

namespace PCL.Core.Minecraft.Profile.Authentication.Utils;

public static class MojangUtils
{
    #region "API 定义"
    
    private const string MojangAuthenticateEndpoint = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string LicenseEndpoint = "https://api.minecraftservices.com/entitlements/mcstore";
    private const string ProfileEndpont = "https://api.minecraftservices.com/minecraft/profile";
    private const string SessionServerProfileEndpoint = "https://sessionserver.mojang.com/session/minecraft/profile/";
    private const string NameProfileEndpoint = "https://api.minecraftservices.com/users/profiles/minecraft/";
    private const string SkinServiceChangeEndpoint = "https://api.minecraftservices.com/minecraft/profile/skins";
    private const string SkinServiceResetEndpoint = "https://api.minecraftservices.com/user/profile/skin/active";

    #endregion
    
    /// <summary>
    /// 发送一次登录请求
    /// </summary>
    /// <param name="xstsToken">XSTS 令牌</param>
    /// <param name="userHash">Xbox User Hash</param>
    /// <returns><see cref="MojangResponse"/></returns>
    public static async Task<MojangResponse?> AuthenticateAsync(string xstsToken, string userHash)
    {
        using var response = await HttpRequest.CreatePost(MojangAuthenticateEndpoint)
            .WithContent(new StringContent(
                JsonSerializer.Serialize(new MojangIdentityToken
                {
                    IdentityToken = $"XBL3.0 x={userHash};{xstsToken}"
                }
                ))
            ).SendAsync().ConfigureAwait(false);
        return await response.AsJsonAsync<MojangResponse>().ConfigureAwait(false);
    }
    
    /// <summary>
    /// 检查玩家是否持有许可
    /// </summary>
    /// <param name="profile"><see cref="SafeProfile"/></param>
    /// <returns>如果没有有效许可，返回 false</returns>
    public static async Task<bool> CheckLicenseAsync(SafeProfile profile)
    {
        using var response = await HttpRequest.Create(LicenseEndpoint)
            .WithAuthentication(profile.TokenType, profile.AccessToken)
            .SendAsync().ConfigureAwait(false);
        return response.IsSuccess;
    }
    /// <summary>
    /// 获取玩家的完整档案
    /// </summary>
    /// <returns>档案信息</returns>
    public static async Task<MojangProfile> GetProfileAsync(SafeProfile profile)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 通过玩家名称获取档案信息 <br/>
    /// NOTE: 由于 Mojang API 限制，此方法仅能获取精简版档案信息 <br/>
    /// 如果需要完整信息，请调用 <see cref="GetFullProfileWithUuidAsync"/>。
    /// </summary>
    /// <param name="name"></param>
    public static async Task GetSimpleProfileWithNameAsync(string name)
    {
        
    }

    public static async Task GetFullProfileWithUuidAsync()
    {
        
    }
}