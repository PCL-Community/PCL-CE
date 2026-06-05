using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeCategories(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("gameId")] int GameId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("iconUrl")] string IconUrl,
    [property: JsonPropertyName("dateModified")] string DateModified,
    [property: JsonPropertyName("isClass")] bool IsClass,
    [property: JsonPropertyName("classId")] int ClassId,
    [property: JsonPropertyName("parentCategoryId")] int ParentCategoryId,
    [property: JsonPropertyName("displayIndex")] int DisplayIndex);
