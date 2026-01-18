using System;

namespace PCL.Core.Model.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeScreenshots(
    int id,
    int modId,
    string title,
    string description,
    string thumbnailUrl);