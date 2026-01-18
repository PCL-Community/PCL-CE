using System.Text.Json.Serialization;

namespace PCL.Core.Link.EasyTier;

// ReSharper disable InconsistentNaming
public record ETPeerInfo
{
    [JsonPropertyName("hostname")] public required string Hostname { get; init; }
    [JsonPropertyName("ipv4")] public required string Ipv4 { get; init; }
    [JsonPropertyName("cost")] public required string Cost { get; init; }
    [JsonPropertyName("lat_ms")] public required string Ping { get; init; }
    [JsonPropertyName("loss_rate")] public required string Loss { get; init; }
    [JsonPropertyName("nat_type")] public required string NatType { get; init; }
    [JsonPropertyName("version")] public required string ETVersion { get; init; }
}
