using System.Text.Json.Serialization;

namespace PCL.Core.Model.Tools.News;

public class ApiResponse
{
    [JsonPropertyName("result")]
    public required Result Result { get; set; }
}
