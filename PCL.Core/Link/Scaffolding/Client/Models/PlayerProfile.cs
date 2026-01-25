using System.Text.Json.Serialization;

namespace PCL.Core.Link.Scaffolding.Client.Models;

public record PlayerProfile
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("machine_id")] public required string MachineId { get; init; }
    [JsonPropertyName("vendor")] public required string Vendor { get; init; }

    [JsonPropertyName("kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlayerKind? Kind { get; init; }
}