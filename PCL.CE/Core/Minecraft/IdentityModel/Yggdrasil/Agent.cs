using System.Text.Json.Serialization;

namespace PCL.CE.Core.Minecraft.IdentityModel.Yggdrasil;

/// <summary>
/// Yggdrasil Agent
/// </summary>
public record Agent
{
    [JsonPropertyName("name")] public string Name { get; init; } = "minecraft";
    [JsonPropertyName("version")] public int Version { get; init; } = 1;
}