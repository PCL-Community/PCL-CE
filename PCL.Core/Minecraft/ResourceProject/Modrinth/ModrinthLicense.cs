using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record ModrinthLicense(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string? Url);
