using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Model.Tools.News;

public class Result
{
    [JsonPropertyName("results")]
    public required List<NewsItem> Results { get; set; }
    [JsonPropertyName("numFound")]
    public int NumFound { get; set; }
    [JsonPropertyName("page")]
    public int Page { get; set; }
}