using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record class CurseforgeProject(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("gameId")] int GameId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("links")] CurseforgeLinks Links,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("downloadCount")] int DownloadCount,
    [property: JsonPropertyName("isFeatured")] bool IsFeatured,
    [property: JsonPropertyName("primaryCategoryId")] int PrimaryCategoryId,
    [property: JsonPropertyName("categories")] List<CurseforgeCategories> Categories,
    [property: JsonPropertyName("classId")] int ClassId,
    [property: JsonPropertyName("authors")] List<CurseforgeAuthors> Authors,
    [property: JsonPropertyName("logo")] CurseforgePictures Logo,
    [property: JsonPropertyName("screenshots")] List<CurseforgePictures> Screenshots,
    [property: JsonPropertyName("mainFileId")] int MainFileId,
    [property: JsonPropertyName("latestFiles")] object LatestFiles);
