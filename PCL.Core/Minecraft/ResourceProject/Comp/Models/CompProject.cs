using System;
using System.Collections.Generic;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models;

[Serializable]
public sealed record class CompProject(
    string Id,
    string Provider,
    string Slug,
    string Name,
    string Summary,
    string? DescriptionHtml,
    Uri? IconUrl,
    List<string> Categories,
    List<string> GameVersions,
    int DownloadCount,
    int? FollowCount,
    CompProjectType ProjectType,
    string? License,
    DateTime DateCreated,
    DateTime DateModified,
    string? Author,
    Uri? SiteUrl,
    Uri? IssuesUrl,
    Uri? SourceUrl,
    CompProjectStatus Status)
{
    public bool IsMod => ProjectType is CompProjectType.Mod or CompProjectType.Unknown;
    public bool IsModpack => ProjectType == CompProjectType.Modpack;
    public bool IsResourcePack => ProjectType == CompProjectType.ResourcePack;
    public bool IsShader => ProjectType == CompProjectType.Shader;
}
