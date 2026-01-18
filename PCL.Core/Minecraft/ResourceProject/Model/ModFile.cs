using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.ResourceProject.Model;

public class ModFile
{
    // --- 基础标识 ---
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;               // CF: long → string; MR: base62 string
    [JsonPropertyName("projectId")]
    public string ProjectId { get; set; } = string.Empty;        // CF: modId; MR: project_id

    // --- 元信息 ---
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;      // CF: displayName; MR: name
    [JsonPropertyName("versionNumber")]
    public string? VersionNumber { get; set; }                   // MR: version_number; CF: 可从 fileName 解析
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;         // CF: fileName; MR: filename

    // --- 时间与统计 ---
    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; set; }
    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }
    [JsonPropertyName("fileSizeBytes")]
    public long FileSizeBytes { get; set; }

    // --- 游戏与加载器 ---
    [JsonPropertyName("gameVersions")]
    public string[] GameVersions { get; set; } = [];      // e.g. ["1.16.5", "1.20.1"]
    [JsonPropertyName("loaders")]
    public string[] Loaders { get; set; } = [];           // e.g. ["fabric", "forge"]

    // --- 状态与类型 ---
    [JsonPropertyName("releaseType")]
    public string ReleaseType { get; set; } = "release";         // "release", "beta", "alpha"
    [JsonPropertyName("status")]
    public string Status { get; set; } = "listed";               // "listed", "archived", etc.
    [JsonPropertyName("isAvailable")]
    public bool IsAvailable { get; set; } = true;                // 推导自 status

    // --- 文件资源 ---
    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;
    [JsonPropertyName("isPrimary")]
    public bool IsPrimary { get; set; } = false;                 // MR: primary; CF: 可设为 true（单文件）

    // --- 哈希校验（标准化为 list of { algo, value }）---
    [JsonPropertyName("hashes")]
    public FileHash[] Hashes { get; set; } = [];

    // --- 依赖项（统一为 { projectId, versionId?, relation }）---
    [JsonPropertyName("dependencies")]
    public ModDependency[] Dependencies { get; set; } = [];

    // --- 可选：变更日志 ---
    [JsonPropertyName("changelog")]
    public string? Changelog { get; set; }
}