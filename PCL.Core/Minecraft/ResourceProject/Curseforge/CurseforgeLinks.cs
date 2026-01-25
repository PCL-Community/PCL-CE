using System;

namespace PCL.Core.Minecraft.ResourceProject.Curseforge;

[Serializable]
public record CurseforgeLinks(
    string websiteUrl,
    string wikiUrl,
    string issuesUrl,
    string sourceUrl);