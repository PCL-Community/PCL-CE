using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeSortableGameVersion(
    [property: JsonPropertyName("gameVersionName")] string? GameVersionName,
    [property: JsonPropertyName("gameVersionPadded")] string? GameVersionPadded,
    [property: JsonPropertyName("gameVersion")] string? GameVersion,
    [property: JsonPropertyName("gameVersionReleaseDate")] string? GameVersionReleaseDate,
    [property: JsonPropertyName("gameVersionTypeId")] int? GameVersionTypeId,
    [property: JsonPropertyName("modLoader")] int? ModLoader);
