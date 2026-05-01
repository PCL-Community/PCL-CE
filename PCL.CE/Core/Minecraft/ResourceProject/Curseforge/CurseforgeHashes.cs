using System;

namespace PCL.CE.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeHashes(
    string value,
    int algo);