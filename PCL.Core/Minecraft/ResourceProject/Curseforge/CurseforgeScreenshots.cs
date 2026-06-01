using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeScreenshots(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("thumbnailUrl")] string ThumbnailUrl);
