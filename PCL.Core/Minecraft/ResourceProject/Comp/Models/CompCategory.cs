using System;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models;

[Serializable]
public sealed record class CompCategory(
    string Id,
    string Name,
    string Slug,
    Uri? IconUrl,
    string? ParentId,
    string? ClassId);
