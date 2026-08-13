using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// Modrinth 格式整合包的清单（<c>modrinth.index.json</c>）模型。
/// 使用 <see cref="JsonCompat"/> 的宽松反序列化读取；缺失的字段保持为 null/空，由安装流程负责校验。
/// </summary>
public sealed class ModrinthManifest
{
    /// <summary>
    /// 清单格式版本号，固定为 <c>1</c>。
    /// </summary>
    public int FormatVersion { get; init; }

    /// <summary>
    /// 目标游戏，固定为 <c>"minecraft"</c>。
    /// </summary>
    public string? Game { get; init; }

    /// <summary>
    /// 该整合包版本的唯一标识符。
    /// </summary>
    public string? VersionId { get; init; }

    /// <summary>
    /// 整合包名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 简短描述（可选）。
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 依赖声明，键如 <c>minecraft</c>、<c>fabric-loader</c>、<c>forge</c>、<c>neoforge</c>、<c>quilt-loader</c>；
    /// 缺失时表示清单未声明依赖。
    /// </summary>
    public Dictionary<string, string>? Dependencies { get; init; }

    /// <summary>
    /// 需要下载的文件列表；缺失或为空表示没有需要联网下载的文件。
    /// </summary>
    public List<ModrinthFile>? Files { get; init; }

    /// <summary>
    /// 按键名（大小写不敏感）获取依赖版本，如 <c>minecraft</c>、<c>fabric-loader</c>。
    /// </summary>
    /// <param name="key">依赖键名。</param>
    /// <returns>依赖的版本值；不存在时返回 null。</returns>
    public string? GetDependency(string key)
    {
        if (Dependencies is null)
            return null;
        foreach (var dependency in Dependencies)
            if (string.Equals(dependency.Key, key, StringComparison.OrdinalIgnoreCase))
                return dependency.Value;
        return null;
    }

    /// <summary>
    /// 宽松解析 Modrinth 清单。
    /// </summary>
    /// <param name="node">清单 JSON 节点。</param>
    /// <returns>解析后的清单；传入 null 时返回 null。</returns>
    public static ModrinthManifest? Parse(JsonNode? node)
    {
        return node.ToObject<ModrinthManifest>();
    }
}

/// <summary>
/// Modrinth 清单中的下载文件项。
/// </summary>
public sealed class ModrinthFile
{
    /// <summary>
    /// 文件在 Minecraft 实例中的相对路径，如 <c>mods/MyMod.jar</c>。不允许包含 <c>..</c> 或以盘符开头。
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 文件哈希校验信息。
    /// </summary>
    public ModrinthHashes? Hashes { get; init; }

    /// <summary>
    /// 客户端/服务端适用性声明。
    /// </summary>
    public ModrinthEnv? Env { get; init; }

    /// <summary>
    /// 文件下载 URL 列表（通常只有一个）。
    /// </summary>
    public List<string>? Downloads { get; init; }

    /// <summary>
    /// 文件大小（字节，可选）。
    /// </summary>
    public long FileSize { get; init; }
}

/// <summary>
/// Modrinth 文件哈希校验信息。
/// </summary>
public sealed class ModrinthHashes
{
    /// <summary>
    /// SHA1 哈希值。
    /// </summary>
    public string? Sha1 { get; init; }

    /// <summary>
    /// SHA512 哈希值。
    /// </summary>
    public string? Sha512 { get; init; }
}

/// <summary>
/// Modrinth 文件的环境适用性声明。
/// </summary>
public sealed class ModrinthEnv
{
    /// <summary>
    /// 客户端适用性，取值为 <c>required</c> / <c>optional</c> / <c>unsupported</c>。
    /// </summary>
    public string? Client { get; init; }

    /// <summary>
    /// 服务端适用性，取值为 <c>required</c> / <c>optional</c> / <c>unsupported</c>。
    /// </summary>
    public string? Server { get; init; }
}
