using System;
using System.Text.Json.Serialization;

namespace PCL.Core.Model.Tools.News;

public class NewsItem
{
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("url")]
    public required string Url { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("image")]
    public required string Image { get; set; }

    [JsonPropertyName("imageAltText")]
    public string? ImageAltText { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; } // Unix 时间戳（秒）

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    public DateTime PublishDate => DateTimeOffset.FromUnixTimeSeconds(Time).LocalDateTime;
}
