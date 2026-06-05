using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record ModrinthGallery(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("featured")] bool Featured,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("created")] string Created,
    [property: JsonPropertyName("ordering")] int Ordering);
