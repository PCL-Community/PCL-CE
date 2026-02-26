using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Link.McPing.Model;

public record McPingModInfoResult(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("modList")] List<McPingModInfoModResult> ModList);