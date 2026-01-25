using System.Text.Json.Serialization;

namespace PCL.Core.Link.Scaffolding.EasyTier;

public record StunInfo
{
    [JsonPropertyName("udp_nat_type")] public required int UdpNatType { get; init; }
    [JsonPropertyName("tcp_nat_type")] public required int TcpNatType { get; init; }
    [JsonPropertyName("public_ip")] public required string[] Ips { get; init; }
}