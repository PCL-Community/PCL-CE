using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Model;

public class ModProject
{
    // --- 基础标识 ---
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    // --- 描述信息 ---
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    // --- 时间信息 ---
    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    // --- 统计数据 ---
    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }
    [JsonPropertyName("favoriteCount")]
    public int FavoriteCount { get; set; }

    // --- 状态 ---
    [JsonPropertyName("status")]
    public string Status { get; set; } = "unknown";

    // --- 分类 ---
    [JsonPropertyName("categories")]
    public string[] Categories { get; set; } = [];

    // --- 作者信息（简化）---
    [JsonPropertyName("authorNames")]
    public string[] AuthorNames { get; set; } = [];

    // --- 资源链接 ---
    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }
    [JsonPropertyName("screenshotUrls")]
    public string[] ScreenshotUrls { get; set; } = [];

    // --- 外部链接 ---
    [JsonPropertyName("directLink")]
    public string DirectLink { get; set; } = string.Empty;

    // --- 版本/文件（简化 ID 列表）---
    [JsonPropertyName("versionIds")]
    public ModFile[] VersionIds { get; set; } = [];
}