using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Model;

public class FileHash
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = string.Empty; // e.g. "sha1", "sha512"
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}