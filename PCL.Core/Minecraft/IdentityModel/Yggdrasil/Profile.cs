using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.IdentityModel.Yggdrasil;

public record Profile
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
}

public record PlayerProperty
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("value")] public required string Value { get; init; }
    [JsonPropertyName("signature")] public string? Signature { get; init; }
}

public record PlayerTextureProperty
{
    [JsonPropertyName("timestamp")] public required long Timestamp { get; init; }
    [JsonPropertyName("profileId")] public required string ProfileId { get; init; }
    [JsonPropertyName("profileName")] public required string ProfileName { get; init; }
    [JsonPropertyName("textures")] public required PlayerTextures Textures { get; init; }
}

public record PlayerTextures
{
    [JsonPropertyName("skin")] public required PlayerTexture Skin { get; init; }
    [JsonPropertyName("cape")] public required PlayerTexture Cape { get; init; }
}

public record PlayerTexture
{
    [JsonPropertyName("Url")] public required string Url { get; init; }
    [JsonPropertyName("metadata")] public required PlayerTextureMetadata Metadata { get; init; }
}

public record PlayerTextureMetadata
{
    [JsonPropertyName("model")] public required string Model { get; init; }
}