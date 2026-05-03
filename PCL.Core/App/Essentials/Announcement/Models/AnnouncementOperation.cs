using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.App.Essentials.Announcement.Models;

public record AnnouncementOperation
{
    /// <summary>
    /// 按钮文本
    /// </summary>
    [JsonPropertyName("text")]
    public required string ButtonText { get; init; }
    
    /// <summary>
    /// 按下后的操作
    /// </summary>
    [JsonPropertyName("exec")]
    public required string Operation { get; init; }
    
    /// <summary>
    /// 参数列表
    /// </summary>
    [JsonPropertyName("argument")]
    public required string Argument { get; init; }
}