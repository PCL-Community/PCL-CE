using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Modpack.Manifests;

/// <summary>
/// 服务端整合包清单（<c>server-manifest.json</c>）。
/// <para>该格式由 HMCL 自创，无官方规范。</para>
/// </summary>
public sealed class ServerManifest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>文件下载基地址，必填。</summary>
    [JsonPropertyName("fileApi")]
    public string? FileApi { get; set; }

    /// <summary>文件列表，必填。</summary>
    [JsonPropertyName("files")]
    public List<ServerManifestFile>? Files { get; set; }

    /// <summary>组件列表，标识取值见 <see cref="ModpackAddonCatalog"/>。</summary>
    [JsonPropertyName("addons")]
    public List<ModpackAddon>? Addons { get; set; }
}

/// <summary>
/// 服务端整合包清单中的文件条目。
/// </summary>
public sealed class ServerManifestFile
{
    /// <summary>相对于实例目录的目标路径。</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>SHA-1 校验值。</summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    /// <summary>可选的直接下载地址；缺失时由 <c>fileApi</c> 拼接得到。</summary>
    [JsonPropertyName("downloadURL")]
    public string? DownloadUrl { get; set; }
}
