using System.Text.Json.Serialization;

namespace PCL.CE.Core.Link.McPing.Model;

public record McPingModInfoModResult(
    [property: JsonPropertyName("modid")] string Id,
    [property: JsonPropertyName("version")] string Version);