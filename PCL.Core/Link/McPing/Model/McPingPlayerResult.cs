using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Link.McPing.Model;

public record McPingPlayerResult(
    [property: JsonPropertyName("max")] int Max,
    [property: JsonPropertyName("online")] int Online,
    [property: JsonPropertyName("sample")] List<McPingPlayerSampleResult>? Samples);