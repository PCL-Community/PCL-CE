using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PCL.Core.Minecraft.Modpack.Manifests;

/// <summary>
/// MCBBS 整合包清单（<c>mcbbs.packmeta</c>，或含 <c>addons</c> 字段的 <c>manifest.json</c>）。
/// <para>该格式无官方规范，字段依 HMCL 的实现整理。</para>
/// </summary>
public sealed class McbbsPackMeta
{
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

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary><c>addon</c> 类型文件的下载基地址。</summary>
    [JsonPropertyName("fileApi")]
    public string? FileApi { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("forceUpdate")]
    public bool ForceUpdate { get; set; }

    [JsonPropertyName("origin")]
    public List<McbbsOrigin>? Origin { get; set; }

    /// <summary>组件列表，必须存在 —— 这是与 CurseForge 格式的判别依据。</summary>
    [JsonPropertyName("addons")]
    public List<ModpackAddon>? Addons { get; set; }

    /// <summary>
    /// 额外的库文件，字段结构与官方版本 JSON 的 <c>libraries</c> 一致，
    /// 因此保留为原始节点交由版本 JSON 层处理。
    /// </summary>
    [JsonPropertyName("libraries")]
    public JsonArray? Libraries { get; set; }

    [JsonPropertyName("files")]
    public List<McbbsPackFile>? Files { get; set; }

    [JsonPropertyName("settings")]
    public McbbsSettings? Settings { get; set; }

    [JsonPropertyName("launchInfo")]
    public McbbsLaunchInfo? LaunchInfo { get; set; }
}

/// <summary>
/// MCBBS 清单的来源标注。
/// </summary>
public sealed class McbbsOrigin
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

/// <summary>
/// MCBBS 清单中的文件条目。
/// <para>
/// 原格式是以 <c>type</c> 区分的多态类型（<c>addon</c> / <c>curse</c>）。
/// 这里合并为单一 DTO：多态反序列化对未知 <c>type</c> 会直接失败，
/// 而清单来自不可信来源，逐字段容错比严格建模更实用。
/// </para>
/// </summary>
public sealed class McbbsPackFile
{
    /// <summary><c>addon</c> 或 <c>curse</c>。</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>是否强制覆盖用户已修改的同名文件。</summary>
    [JsonPropertyName("force")]
    public bool Force { get; set; }

    // type = "addon"

    /// <summary>相对于实例目录的目标路径。</summary>
    [JsonPropertyName("path")]
    public string? Path { get; set; }

    /// <summary>SHA-1 校验值。</summary>
    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    // type = "curse"

    [JsonPropertyName("projectID")]
    public int? ProjectId { get; set; }

    [JsonPropertyName("fileID")]
    public int? FileId { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

/// <summary>
/// MCBBS 清单的安装开关。
/// </summary>
public sealed class McbbsSettings
{
    [JsonPropertyName("install_mods")]
    public bool InstallMods { get; set; } = true;

    [JsonPropertyName("install_resourcepack")]
    public bool InstallResourcePack { get; set; } = true;
}

/// <summary>
/// MCBBS 清单的启动信息。
/// </summary>
public sealed class McbbsLaunchInfo
{
    /// <summary>最小内存（MB）。</summary>
    [JsonPropertyName("minMemory")]
    public int? MinMemory { get; set; }

    /// <summary>支持的 Java 主版本号。</summary>
    [JsonPropertyName("supportJava")]
    public List<int>? SupportJava { get; set; }

    [JsonPropertyName("launchArgument")]
    public List<string>? LaunchArgument { get; set; }

    [JsonPropertyName("javaArgument")]
    public List<string>? JavaArgument { get; set; }
}
