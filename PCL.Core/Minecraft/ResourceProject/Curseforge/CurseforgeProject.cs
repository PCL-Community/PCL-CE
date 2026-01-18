using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record class CurseforgeProject(
    int id,
    int gameId,
    string name,
    string slug,
    CurseforgeLinks links,
    string summary,
    int status,
    int downloadCount,
    bool isFeatured,
    int primaryCategoryId,
    List<CurseforgeCategories> categories,
    int classId,
    List<CurseforgeAuthors> authors,
    CurseforgePictures logo,
    List<CurseforgePictures> screenshots,
    int mainFileId,
    object latestFiles);