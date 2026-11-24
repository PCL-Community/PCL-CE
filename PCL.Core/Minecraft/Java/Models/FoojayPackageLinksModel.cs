using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Java.Models;

public record FoojayPackageLinksModel
{
    [JsonPropertyName("pkg_info_uri")]
    public required string Info { get; set; }

    [JsonPropertyName("pkg_download_redirect")]
    public required string DownloadRedirect { get; set; }
}
