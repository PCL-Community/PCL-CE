using System.Text.Json.Serialization;

namespace PCL;

public class VersionAnnouncementDataModel
{
    [JsonPropertyName("content")]
    public List<VersionAnnouncementContentModel> Content { get; set; }
}

public class VersionAnnouncementContentModel
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("detail")]
    public string Detail { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("btn1")]
    public AnnouncementBtnInfoModel Btn1 { get; set; }

    [JsonPropertyName("btn2")]
    public AnnouncementBtnInfoModel Btn2 { get; set; }
}

public class AnnouncementBtnInfoModel
{
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("command")]
    public string Command { get; set; }

    [JsonPropertyName("command_paramter")]
    public string CommandParameter { get; set; }
}