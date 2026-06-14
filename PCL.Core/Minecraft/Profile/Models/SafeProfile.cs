using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Models;

public record SafeProfile
{
    [JsonPropertyName("username")]
    public required string UserName { get; set; }
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; set; }
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }
    [JsonPropertyName("tokenType")]
    public required string TokenType { get; set; }
}