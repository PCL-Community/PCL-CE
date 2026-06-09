using System.Text.Json.Serialization;
using PCL.Core.Minecraft.Profile.Autnenrication;

namespace PCL.Core.Minecraft.Profile.Models;

internal class Profile
{
    /// <summary>
    /// 档案名称
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }
    /// <summary>
    /// UUID
    /// </summary>
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }
    /// <summary>
    /// 访问令牌
    /// </summary>
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("refreshToken")]
    public required string RefreshToken { get; set; }
    [JsonPropertyName("propertyJson")]
    public required string PropertyJson { get; set; }
    [JsonPropertyName("profileDescription")]
    public required string ProfileDescription { get; set; }

    [JsonPropertyName("profileType")] 
    public required ProfileType Type { get; set; }

    public IAuthenticateProvider? CreateAuthenticateServiceProvider() => Type switch
    {
        ProfileType.Offline => new OfflineProvider(AccessToken, RefreshToken),
        ProfileType.Microsoft => new MicrosoftProvider(AccessToken, RefreshToken),
        ProfileType.Authlib => new YggdrasilProvider(AccessToken, RefreshToken)
    }
}