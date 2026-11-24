using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Java.Models;

public record FoojayDistributionModel
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("api_parameter")]
    public required string ApiParameter { get; set; }

    [JsonPropertyName("vendor")]
    public required string Vendor { get; set; }

    [JsonPropertyName("available")]
    public required bool Available { get; set; }
    
    [JsonPropertyName("versions")]
    public required string[] Versions { get; set; }
}
