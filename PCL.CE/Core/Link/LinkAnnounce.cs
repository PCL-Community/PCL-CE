using System.Text.Json.Serialization;

namespace PCL.CE.Core.Link;

public enum LinkAnnounceType
{
    [JsonStringEnumMemberName("notice")] Notice,
    [JsonStringEnumMemberName("warning")] Warning,
    [JsonStringEnumMemberName("important")] Important
}

public record LinkAnnounceInfo(
    [property:JsonPropertyName("type")] LinkAnnounceType Type,
    [property:JsonPropertyName("content")] string Content
);

public record LinkAnnounce(
    [property:JsonPropertyName("notices")] LinkAnnounceInfo[] Announces
);
