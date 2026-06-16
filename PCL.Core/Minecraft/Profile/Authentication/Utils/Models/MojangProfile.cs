using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Profile.Authentication.Utils.Models;

public record MojangProfile
{
    [JsonPropertyName("id")]
    public required string Uuid { get; set; }
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}