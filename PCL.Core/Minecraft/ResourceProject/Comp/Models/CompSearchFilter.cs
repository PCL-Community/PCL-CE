using System.Collections.Generic;
using PCL.Core.Minecraft.ResourceProject.Comp.Models.Enums;

namespace PCL.Core.Minecraft.ResourceProject.Comp.Models;

public sealed record class CompSearchFilter
{
    public string? Query { get; init; }
    public string? GameVersion { get; init; }
    public List<ModLoaderType> Loaders { get; init; } = [];
    public string? Category { get; init; }
    public CompProjectType? ProjectType { get; init; }
    public CompSortField SortField { get; init; } = CompSortField.Relevance;
    public SortOrder SortOrder { get; init; } = SortOrder.Desc;
    public int Offset { get; init; }
    public int Limit { get; init; } = 20;
}
