using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Modpack.Persistence;

/// <summary>
/// 写入实例根目录 <c>modpack.json</c> 的安装记录。
/// <para>
/// 用途有三：识别实例由哪个整合包安装而来、在更新时比对文件变化、
/// 在安装失败时判断哪些文件属于本次安装。字段布局与 HMCL 的同名文件保持一致。
/// </para>
/// </summary>
public sealed class ModpackConfiguration
{
    /// <summary>实例根目录下的记录文件名。</summary>
    public const string FileName = "modpack.json";

    /// <summary>整合包格式标识，取值见 <see cref="ModpackFormatExtensions.ToDisplayName"/>。</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>整合包名称。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>整合包版本。</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    /// <summary>原始清单快照，用于更新时比对文件列表。</summary>
    [JsonPropertyName("manifest")]
    public JsonNode? Manifest { get; set; }

    /// <summary>本次安装释放的覆写文件及其 SHA-1。</summary>
    [JsonPropertyName("overrides")]
    public List<ModpackFileSnapshot> Overrides { get; set; } = [];

    /// <summary>解析出的整合包格式，无法识别时为 <c>null</c>。</summary>
    [JsonIgnore]
    public ModpackFormat? Format => ModpackFormatExtensions.ParseDisplayName(Type);

    /// <summary>
    /// 构建「相对路径 → SHA-1」索引，供更新时快速比对。
    /// </summary>
    public IReadOnlyDictionary<string, string> BuildOverrideIndex()
    {
        var index = new Dictionary<string, string>(Overrides.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in Overrides)
        {
            if (!string.IsNullOrWhiteSpace(snapshot.Path) && !string.IsNullOrWhiteSpace(snapshot.Hash))
                index[snapshot.Path] = snapshot.Hash;
        }

        return index;
    }
}

/// <summary>
/// 一个覆写文件的路径与校验值。
/// </summary>
/// <param name="Path">相对于实例目录的路径。</param>
/// <param name="Hash">文件的 SHA-1（十六进制小写）。</param>
public sealed record ModpackFileSnapshot(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("hash")] string Hash);
