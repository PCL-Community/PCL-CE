using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.App.Essentials.Announcement.Models;

public record AnnouncementDetails
{
    /// <summary>
    /// 公告标题
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    /// <summary>
    /// 公告内容
    /// </summary>
    [JsonPropertyName("details")]
    public required string Details { get; init; }
    
    /// <summary>
    /// 该公告的优先级，值越高优先级越高
    /// </summary>
    [JsonPropertyName("priority")] 
    public int Priority { get; init; }
    
    /// <summary>
    /// 公告 ID
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    /// <summary>
    /// 该公告的等级，决定弹窗应该用什么样式
    /// </summary>
    [JsonPropertyName("level")] 
    public required AnnouncementLevel Level { get; init; }
    
    /// <summary>
    /// 该公告的发布日期
    /// </summary>
    [JsonPropertyName("date")]
    public required string ReleaseDate { get; init; }
    
    /// <summary>
    /// 显示条件
    /// </summary>
    [JsonPropertyName("skip")]
    public required AnnouncementSkipCondition SkipOn { get; init; }
    
    /// <summary>
    /// 弹窗按钮信息
    /// </summary>
    [JsonPropertyName("buttons")]
    public required IEnumerable<AnnouncementOperation> Buttons { get; init; }
}