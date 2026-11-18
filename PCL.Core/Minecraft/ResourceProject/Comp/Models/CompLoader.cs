using System;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models;

[Serializable]
public sealed record class CompLoader(
    string Name,
    string? DisplayName,
    ModLoaderType LoaderType,
    bool IsLatest);
