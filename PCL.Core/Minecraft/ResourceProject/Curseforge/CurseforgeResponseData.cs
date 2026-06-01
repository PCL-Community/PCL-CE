using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record class CurseforgeProjectResponse(
    [property: JsonPropertyName("data")] CurseforgeProject Data);

[Serializable]
public record class CurseforgeProjectsResponse(
    [property: JsonPropertyName("data")] List<CurseforgeProject> Data);
