using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Java.Models;

public record FoojayVersionModel
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("distribution")]
    public required string Distribution { get; set; }

    [JsonPropertyName("major_version")]
    public required int MajorVersion { get; set; }

    [JsonPropertyName("java_version")]
    public required string JavaVersion { get; set; }

    [JsonPropertyName("operating_system")]
    public required string OperatingSystem { get; set; }

    [JsonPropertyName("architecture")]
    public required string Architecture { get; set; }

    [JsonPropertyName("package_type")]
    public required string PackageType { get; set; }

    [JsonPropertyName("filename")]
    public required string Filename { get; set; }

    [JsonPropertyName("links")]
    public required FoojayPackageLinksModel Links { get; set; }
    
    [JsonPropertyName("size")]
    public required long Size { get; set; }

    [JsonPropertyName("archive_type")]
    public required string ArchiveType { get; set; }

    [JsonPropertyName("term_of_support")]
    public required string TermOfSupport { get; set; }

    [JsonPropertyName("release_status")]
    public required string ReleaseStatus { get; set; }

    [JsonPropertyName("latest_build_available")]
    public required bool LatestBuildAvailable { get; set; }

    [JsonPropertyName("directly_downloadable")]
    public required bool DirectlyDownloadable { get; set; }

    [JsonPropertyName("lib_c_type")]
    public string? LibCType { get; set; }
}
