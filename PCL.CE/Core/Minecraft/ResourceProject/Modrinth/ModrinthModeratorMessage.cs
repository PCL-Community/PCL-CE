using System;

namespace PCL.CE.Core.Minecraft.ResourceProject.Modrinth;

[Serializable]
public record ModrinthModeratorMessage(
    string message,
    string? body);