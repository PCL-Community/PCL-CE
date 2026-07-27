using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Modpack.Manifests;

/// <summary>
/// CurseForge 整合包清单（<c>manifest.json</c>）。
/// <para>参考 https://gdlauncher.com/docs/modpack-manifest-format 。</para>
/// </summary>
public sealed class CurseForgeManifest
{
    /// <summary>固定值 <c>minecraftModpack</c>。</summary>
    [JsonPropertyName("manifestType")]
    public string? ManifestType { get; set; }

    [JsonPropertyName("manifestVersion")]
    public int ManifestVersion { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    /// <summary>覆写目录在压缩包内的名称，默认为 <c>overrides</c>。</summary>
    [JsonPropertyName("overrides")]
    public string? Overrides { get; set; }

    [JsonPropertyName("minecraft")]
    public CurseForgeMinecraft? Minecraft { get; set; }

    [JsonPropertyName("files")]
    public List<CurseForgeManifestFile>? Files { get; set; }

    /// <summary>
    /// 该清单是否属于 MCBBS 格式。MCBBS 复用了 <c>manifest.json</c> 文件名，
    /// 以是否存在 <c>addons</c> 字段区分，因此这里保留该字段仅用于判别。
    /// </summary>
    [JsonPropertyName("addons")]
    public List<object>? Addons { get; set; }
}

/// <summary>
/// CurseForge 清单的 <c>minecraft</c> 段。
/// </summary>
public sealed class CurseForgeMinecraft
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("modLoaders")]
    public List<CurseForgeModLoader>? ModLoaders { get; set; }
}

/// <summary>
/// CurseForge 清单中的加载器条目，<c>id</c> 形如 <c>forge-47.2.0</c>。
/// </summary>
public sealed class CurseForgeModLoader
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("primary")]
    public bool Primary { get; set; }
}

/// <summary>
/// CurseForge 清单中的文件条目。
/// <para>
/// <c>fileName</c> 与 <c>url</c> 在导出的清单中通常缺失，需在安装时经 API 补齐。
/// </para>
/// </summary>
public sealed class CurseForgeManifestFile
{
    [JsonPropertyName("projectID")]
    public int ProjectId { get; set; }

    [JsonPropertyName("fileID")]
    public int FileId { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; } = true;

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
