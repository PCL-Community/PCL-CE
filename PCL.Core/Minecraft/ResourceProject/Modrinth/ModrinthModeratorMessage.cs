using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record ModrinthModeratorMessage(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("body")] string? Body);
