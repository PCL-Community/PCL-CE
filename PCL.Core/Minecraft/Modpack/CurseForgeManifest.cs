using System.Collections.Generic;
using System.Text.Json.Nodes;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// CurseForge 格式整合包的清单（<c>manifest.json</c>）模型。
/// 使用 <see cref="JsonCompat"/> 的宽松反序列化读取；缺失的字段保持为 null/空，由安装流程负责校验。
/// </summary>
public sealed class CurseForgeManifest
{
    /// <summary>
    /// 清单类型，固定为 <c>"minecraftModpack"</c>。
    /// </summary>
    public string? ManifestType { get; init; }

    /// <summary>
    /// 清单版本号，当前固定为 <c>1</c>。
    /// </summary>
    public int ManifestVersion { get; init; }

    /// <summary>
    /// 整合包名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 整合包版本号。
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 整合包推荐的启动内存（单位 MB，可选），如 <c>4096</c> 表示 4 GiB；缺失时表示未声明推荐内存。
    /// </summary>
    public int? RecommendedRam { get; init; }

    /// <summary>
    /// 有效的推荐内存：根级 <c>recommendedRam</c> 优先，其次 <c>minecraft.recommendedRam</c>；
    /// 两者都不存在时表示未声明推荐内存。
    /// </summary>
    public int? RecommendedRamEffective => RecommendedRam ?? Minecraft?.RecommendedRam;

    /// <summary>
    /// 作者名称（可选）。
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// 整合包图标路径（可选），通常是压缩包内的相对路径，如 <c>profileImage/xxx.jpg</c>。
    /// </summary>
    public string? Image { get; init; }

    /// <summary>
    /// 游戏相关信息；缺失时表示清单未声明游戏版本。
    /// </summary>
    public CurseForgeMinecraft? Minecraft { get; init; }

    /// <summary>
    /// 需要下载的模组清单；缺失或为空表示没有需要联网下载的文件。
    /// </summary>
    public List<CurseForgeFile>? Files { get; init; }

    /// <summary>
    /// overrides 目录名；缺失时表示整合包未声明覆写目录（与历史行为一致，不默认补全）。
    /// </summary>
    public string? Overrides { get; init; }

    /// <summary>
    /// 宽松解析 CurseForge 清单。
    /// </summary>
    /// <param name="node">清单 JSON 节点。</param>
    /// <returns>解析后的清单；传入 null 时返回 null。</returns>
    public static CurseForgeManifest? Parse(JsonNode? node)
    {
        return node.ToObject<CurseForgeManifest>();
    }
}

/// <summary>
/// CurseForge 清单中的游戏信息。
/// </summary>
public sealed class CurseForgeMinecraft
{
    /// <summary>
    /// Minecraft 版本号，如 <c>"1.20.1"</c>。
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 推荐内存（单位 MB，可选）；部分整合包把 <c>recommendedRam</c> 放在 <c>minecraft</c> 对象内而非根级。
    /// </summary>
    public int? RecommendedRam { get; init; }

    /// <summary>
    /// 加载器列表，如 <c>forge-47.2.0</c>、<c>fabric-0.15.11</c>。
    /// </summary>
    public List<CurseForgeModLoader>? ModLoaders { get; init; }
}

/// <summary>
/// CurseForge 清单中的加载器声明。
/// </summary>
public sealed class CurseForgeModLoader
{
    /// <summary>
    /// 加载器 ID，格式为 <c>&lt;loader&gt;-&lt;version&gt;</c>。
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// 是否为主加载器。
    /// </summary>
    public bool Primary { get; init; }
}

/// <summary>
/// CurseForge 清单中需要下载的文件项。
/// </summary>
public sealed class CurseForgeFile
{
    /// <summary>
    /// CurseForge 项目 ID；缺失时表示无法解析该文件项。
    /// </summary>
    public int? ProjectId { get; init; }

    /// <summary>
    /// CurseForge 文件 ID；缺失时表示无法解析该文件项。
    /// </summary>
    public int? FileId { get; init; }

    /// <summary>
    /// 是否为必需文件；缺失时视为必需。
    /// </summary>
    public bool? Required { get; init; }

    /// <summary>
    /// 是否为可选文件（<c>required</c> 显式为 false）。
    /// </summary>
    public bool IsOptional => Required is false;
}
