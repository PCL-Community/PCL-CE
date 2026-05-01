using System;

namespace PCL.CE.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgePictures(
    int id,
    int modId,
    string title,
    string description,
    string thumbnailUrl,
    string url);