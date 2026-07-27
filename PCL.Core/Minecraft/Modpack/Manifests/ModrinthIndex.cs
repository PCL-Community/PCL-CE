using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Modpack.Manifests;

/// <summary>
/// Modrinth 整合包索引（<c>modrinth.index.json</c>）。
/// <para>参考 https://support.modrinth.com/en/articles/8802351-modrinth-modpack-format-mrpack 。</para>
/// </summary>
public sealed class ModrinthIndex
{
    /// <summary>格式版本，当前为 <c>1</c>。</summary>
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; }

    /// <summary>目标游戏，当前仅 <c>minecraft</c>。</summary>
    [JsonPropertyName("game")]
    public string? Game { get; set; }

    /// <summary>整合包版本标识符。</summary>
    [JsonPropertyName("versionId")]
    public string? VersionId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("files")]
    public List<ModrinthIndexFile>? Files { get; set; }

    /// <summary>
    /// 依赖表。必须含 <c>minecraft</c>；其余键为加载器标识，
    /// 见 <see cref="Providers.ModrinthModpackProvider"/> 中的映射。
    /// </summary>
    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; set; }
}

/// <summary>
/// Modrinth 索引中的文件条目。
/// </summary>
public sealed class ModrinthIndexFile
{
    /// <summary>相对于实例目录的目标路径。</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>校验值表，规范要求同时包含 <c>sha1</c> 与 <c>sha512</c>。</summary>
    [JsonPropertyName("hashes")]
    public Dictionary<string, string>? Hashes { get; set; }

    [JsonPropertyName("env")]
    public ModrinthFileEnvironment? Env { get; set; }

    /// <summary>下载地址列表，规范要求为 HTTPS。</summary>
    [JsonPropertyName("downloads")]
    public List<string>? Downloads { get; set; }

    [JsonPropertyName("fileSize")]
    public long? FileSize { get; set; }
}

/// <summary>
/// Modrinth 文件的环境要求，取值为 <c>required</c> / <c>optional</c> / <c>unsupported</c>。
/// </summary>
public sealed class ModrinthFileEnvironment
{
    [JsonPropertyName("client")]
    public string? Client { get; set; }

    [JsonPropertyName("server")]
    public string? Server { get; set; }
}
