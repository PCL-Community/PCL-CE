using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Models;

internal record OldProfile: SafeProfile
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
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