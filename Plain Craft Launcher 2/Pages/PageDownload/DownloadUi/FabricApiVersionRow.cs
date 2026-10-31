namespace PCL;

/// <summary>Fabric API 版本列表的行数据。</summary>
public sealed class FabricApiVersionRow
{
    public required string Title { get; init; }

    public required string Info { get; init; }

    public required string Logo { get; init; }

    public required Comp.CompFile Entry { get; init; }
}
