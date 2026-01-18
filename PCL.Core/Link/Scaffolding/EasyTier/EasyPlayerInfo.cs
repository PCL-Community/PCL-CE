namespace PCL.Core.Link.Scaffolding.EasyTier;

public enum ConnectionWay
{
    Local,
    P2P,
    Relay,
    Unknown
}

public record EasyPlayerInfo
{
    public required bool IsHost { get; init; }
    public required string HostName { get; init; }
    public required string Ip { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string MinecraftName { get; init; } = string.Empty;
    public ConnectionWay Way { get; init; } = ConnectionWay.Unknown;
    public double Ping { get; init; }
    public double Loss { get; init; }
    public string NatType { get; init; } = string.Empty;
    public string EasyTierVer { get; init; } = string.Empty;
}