using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Modpack.Manifests;

/// <summary>
/// MultiMC / Prism Launcher 的组件清单（<c>mmc-pack.json</c>）。
/// </summary>
public sealed class MultiMcPack
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    [JsonPropertyName("components")]
    public List<MultiMcComponent>? Components { get; set; }
}

/// <summary>
/// <c>mmc-pack.json</c> 中的一个组件引用。
/// </summary>
public sealed class MultiMcComponent
{
    /// <summary>组件唯一标识，取值见 <see cref="MultiMc.MultiMcComponentCatalog"/>。</summary>
    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("cachedName")]
    public string? CachedName { get; set; }

    [JsonPropertyName("cachedVersion")]
    public string? CachedVersion { get; set; }

    /// <summary>允许自动更新。</summary>
    [JsonPropertyName("cachedVolatile")]
    public bool CachedVolatile { get; set; }

    /// <summary>仅作为其他组件的依赖存在，不应视为用户显式选择的组件。</summary>
    [JsonPropertyName("dependencyOnly")]
    public bool DependencyOnly { get; set; }

    /// <summary>是否为关键组件。</summary>
    [JsonPropertyName("important")]
    public bool Important { get; set; }

    /// <summary>已缓存的依赖声明。</summary>
    [JsonPropertyName("cachedRequires")]
    public List<MultiMcRequirement>? CachedRequires { get; set; }

    /// <summary>解析实际生效的版本号 —— <c>version</c> 缺失时回退到 <c>cachedVersion</c>。</summary>
    public string? ResolveVersion() =>
        !string.IsNullOrWhiteSpace(Version) ? Version :
        !string.IsNullOrWhiteSpace(CachedVersion) ? CachedVersion : null;
}

/// <summary>
/// 组件依赖声明。
/// </summary>
public sealed class MultiMcRequirement
{
    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    /// <summary>要求版本严格等于该值。</summary>
    [JsonPropertyName("equals")]
    public string? RequiredVersion { get; set; }

    /// <summary>建议使用的版本。</summary>
    [JsonPropertyName("suggests")]
    public string? Suggests { get; set; }
}
