using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Link.Scaffolding.EasyTier;

internal record PublicNodeDto
{
    [JsonPropertyName("success")] public bool IsSuccess { get; init; }
    [JsonPropertyName("data")] public required NodeDataDto Data { get; init; }
}

internal record NodeDataDto
{
    [JsonPropertyName("items")] public required IReadOnlyList<NodeItemDto> Items { get; init; }
}

internal record NodeItemDto
{
    [JsonPropertyName("address")] public required string Host { get; init; }
    [JsonPropertyName("allow_relay")] public bool IsAllowRelay { get; init; }
    [JsonPropertyName("is_active")] public bool IsActive { get; init; }
}