using System.Collections.Generic;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models;

public sealed record class CompSearchResult(
    List<CompProject> Hits,
    int TotalCount,
    int Offset,
    int Limit);
