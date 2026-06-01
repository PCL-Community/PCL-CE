using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeHashes(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("algo")] int Algo);
