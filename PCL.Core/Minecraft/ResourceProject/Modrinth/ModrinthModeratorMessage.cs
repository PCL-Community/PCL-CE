using System;

namespace PCL.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record ModrinthModeratorMessage(
    string message,
    string? body);