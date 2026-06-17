using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Models;

/// <summary>
/// 适用于外部使用的安全档案类
/// </summary>
public record SafeProfile
{
    [JsonPropertyName("username")]
    public required string UserName { get; set; }
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }
    [JsonPropertyName("tokenType")]
    public string? TokenType { get; set; }
    [JsonPropertyName("profileType")]
    public ProfileType? ProfileType { get; set; }

    private McProfile? _innerProfile;
}