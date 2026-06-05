using System;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models;

[Serializable]
public sealed record class CompGameVersion(
    string Id,
    string Version,
    string? VersionType,
    DateTime? DateModified);
