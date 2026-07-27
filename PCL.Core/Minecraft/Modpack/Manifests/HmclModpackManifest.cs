using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Modpack.Manifests;

/// <summary>
/// HMCL 自有整合包格式的清单（<c>modpack.json</c>）。
/// </summary>
public sealed class HmclModpackManifest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// 游戏版本。该字段常缺失，此时需从 <c>minecraft/pack.json</c> 推断。
    /// </summary>
    [JsonPropertyName("gameVersion")]
    public string? GameVersion { get; set; }

    /// <summary>整合包文件列表，字段与 <c>modpack.json</c> 的 <c>overrides</c> 记录一致。</summary>
    [JsonPropertyName("fileApi")]
    public string? FileApi { get; set; }
}

/// <summary>
/// HMCL 整合包内嵌的实例定义（<c>minecraft/pack.json</c>）。
/// <para>
/// 这是 HMCL 的版本描述格式：以 <c>patches</c> 列表记录游戏与各加载器，
/// 其 <c>id</c> 取值与 MCBBS 的 <c>addons</c> 相同。
/// </para>
/// </summary>
public sealed class HmclPackDefinition
{
    /// <summary>主 JAR 对应的游戏版本。</summary>
    [JsonPropertyName("jar")]
    public string? Jar { get; set; }

    [JsonPropertyName("patches")]
    public List<HmclPackPatch>? Patches { get; set; }
}

/// <summary>
/// <c>pack.json</c> 中的一个组件。
/// </summary>
public sealed class HmclPackPatch
{
    /// <summary>组件标识，取值见 <see cref="ModpackAddonCatalog"/>。</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}
