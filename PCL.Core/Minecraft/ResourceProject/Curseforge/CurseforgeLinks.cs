using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeLinks(
    [property: JsonPropertyName("websiteUrl")] string WebsiteUrl,
    [property: JsonPropertyName("wikiUrl")] string WikiUrl,
    [property: JsonPropertyName("issuesUrl")] string IssuesUrl,
    [property: JsonPropertyName("sourceUrl")] string SourceUrl);
