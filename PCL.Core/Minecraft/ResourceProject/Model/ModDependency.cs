using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Model;

public class ModDependency
{
    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;
    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }
    [JsonPropertyName("relationType")]
    public string RelationType { get; set; } = "required";
}