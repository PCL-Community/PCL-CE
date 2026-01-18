using System.Collections.Generic;

namespace PCL.Core.Link.EasyTier;
// ReSharper disable InconsistentNaming

public class ETRelay
{
    public static List<ETRelay> RelayList { get; set; } = [];

    public required string Url { get; init; }
    public required string Name { get; init; }
    public ETRelayType Type { get; init; }
}

public enum ETRelayType
{
    Community,
    Selfhosted,
    Custom
}
