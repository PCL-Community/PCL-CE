using System;

namespace PCL.CE.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeCategories(
    int id,
    int gameId,
    string name,
    string slug,
    string url,
    string iconUrl,
    string dateModified,
    bool isClass,
    int classId,
    int parentCategoryId,
    int displayIndex);