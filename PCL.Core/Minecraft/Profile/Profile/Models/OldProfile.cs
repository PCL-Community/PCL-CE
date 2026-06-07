using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Models;

public record OldProfile
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    [JsonPropertyName("uuid")]
    public required string Uuid { get; set; }
    [JsonPropertyName("username")]
    public required string UserName { get; set; }
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }
    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }
    [JsonPropertyName("expires")]
    public long? Expires { get; set; }
    [JsonPropertyName("desc")]
    public required string Description { get; set; }
    [JsonPropertyName("skinHeadId")]
    public required string SkinHeadId { get; set; }
    [JsonPropertyName("serverName")]
    public string? ServerName { get; set; }
    [JsonPropertyName("server")]
    public string? Server { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("password")]
    public string? Password { get; set; }
    
}