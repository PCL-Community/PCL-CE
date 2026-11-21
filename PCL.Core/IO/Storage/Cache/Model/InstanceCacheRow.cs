using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PCL.Core.IO.Storage.Cache.Model;

public record InstanceCacheRow
{
    public string InstancePath { get; init; } = string.Empty;
    public string InstanceName { get; init; } = string.Empty;
    public string InstanceState { get; init; } = "Error";
    public int CardType { get; init; }
    public bool IsStarred { get; init; }
    public string? Logo { get; init; }
    public string? Description { get; init; }
    public string? ReleaseTime { get; init; }
    public string? VanillaName { get; init; }
    public string? VanillaVersion { get; init; }
    public int DropNumber { get; init; }
    public bool Reliable { get; init; }

    ///<summary>
    /// 加载器版本（统一 JSON 存储，替代 n 组 HasXxx + XxxVersion 字段）<br/>
    /// 格式：[{"type":"forge","version":"47.2.0"},{"type":"fabric","version":"0.15.0"}]<br/>
    /// 空数组 = 无加载器
    /// </summary>
    public string LoaderJson { get; internal set; } = "[]";

    /// <summary>解析加载器列表（反序列化 LoaderJson）</summary>
    public List<LoaderEntry> GetLoaders() =>
        JsonSerializer.Deserialize<List<LoaderEntry>>(LoaderJson) ?? [];

    /// <summary>快速检查是否存在指定类型的加载器</summary>
    public bool HasLoader(string type) =>
        GetLoaders().Any(l => l.Type == type);

    /// <summary>获取指定加载器的版本（不存在返回 null）</summary>
    public string? GetLoaderVersion(string type) =>
        GetLoaders().FirstOrDefault(l => l.Type == type)?.Version;

    /// <summary>将加载器列表序列化为 JSON 赋给 LoaderJson</summary>
    public void SetLoaders(List<LoaderEntry> loaders) =>
        LoaderJson = JsonSerializer.Serialize(loaders);

    // Manifest summary
    public string? MainClass { get; init; }
    public string? AssetsIndex { get; init; }
    public string? InheritsFrom { get; init; }
    public int? JavaVersion { get; init; }

    // Cache control
    public string SourceJsonHash { get; init; } = string.Empty; // version.json SHA256
    public int FormatVersion { get; init; } = 1;
    public DateTime CachedAt { get; init; }
    public DateTime? LastLoadedAt { get; init; }
}

/// <summary>
/// 加载器版本条目——统一模型替代 N 组 HasXxx/XxxVersion 属性。
/// 新增加载器类型只需在此记录中添加新条目，无需修改表结构或模型类。
/// </summary>
public record LoaderEntry
{
    /// <summary>加载器类型标识，如 "forge" / "fabric" / "quilt" / "neoforge" / "liteloader" / "optifine" / "labymod" / "cleanroom" / "legacyfabric"</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>加载器版本号（空字符串 = 已检测到加载器但版本未知，null = 不存在）</summary>
    public string? Version { get; init; }
}