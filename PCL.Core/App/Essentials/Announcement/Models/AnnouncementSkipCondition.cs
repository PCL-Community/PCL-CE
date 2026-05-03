using System.Text.Json.Serialization;

namespace PCL.Core.App.Essentials.Announcement.Models;

public class AnnouncementSkipCondition
{
    [JsonPropertyName("min")]
    public string? MinVersion { get; init; }
    [JsonPropertyName("max")]
    public string? MaxVersion { get; init; }
    [JsonPropertyName("notAfter")]
    public string? NotAfter { get; init; }
    [JsonPropertyName("notBefore")]
    public string? NotBefore { get; init; }

}
