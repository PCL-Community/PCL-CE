using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Java.Models;

public record FoojayPackageInfoModel
{
    [JsonPropertyName("direct_download_uri")]
    public string? DirectDownloadUri { get; set; }

    [JsonPropertyName("checksum")]
    public string? Checksum { get; set; }

    [JsonPropertyName("checksum_type")]
    public string? ChecksumType { get; set; }
}
