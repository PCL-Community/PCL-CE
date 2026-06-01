using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeFileDependency(
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("relationType")] int RelationType);
