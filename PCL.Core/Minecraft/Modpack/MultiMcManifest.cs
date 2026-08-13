using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// MultiMC 格式整合包的清单（<c>mmc-pack.json</c>）模型。
/// 使用 <see cref="JsonCompat"/> 的宽松反序列化读取；缺失的字段保持为 null/空，由安装流程负责校验。
/// </summary>
public sealed class MultiMcManifest
{
    /// <summary>
    /// 清单格式版本号。
    /// </summary>
    public int FormatVersion { get; init; }

    /// <summary>
    /// 组件列表，每个组件引用 <c>patches/</c> 目录下的一个补丁文件。
    /// </summary>
    public List<MultiMcComponent>? Components { get; init; }

    /// <summary>
    /// 宽松解析 mmc-pack.json。
    /// </summary>
    /// <param name="node">清单 JSON 节点。</param>
    /// <returns>解析后的清单；传入 null 时返回 null。</returns>
    public static MultiMcManifest? Parse(JsonNode? node)
    {
        return node.ToObject<MultiMcManifest>();
    }

    /// <summary>
    /// 从 <c>instance.cfg</c> 文本中解析实例显示名称。
    /// 兼容 CRLF / LF 换行，并忽略 <c>name</c> 键两侧的空白。
    /// </summary>
    /// <param name="instanceCfgText">instance.cfg 的完整文本。</param>
    /// <returns>实例名称；未找到时返回 null。</returns>
    public static string? ParseInstanceName(string? instanceCfgText)
    {
        if (string.IsNullOrEmpty(instanceCfgText))
            return null;
        foreach (var rawLine in instanceCfgText.Split(new[] { "\r\n", "\n", "\r" },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = rawLine.IndexOf('=');
            if (separatorIndex < 0)
                continue;
            var key = rawLine[..separatorIndex].Trim();
            if (!string.Equals(key, "name", StringComparison.OrdinalIgnoreCase))
                continue;
            var value = rawLine[(separatorIndex + 1)..].Trim();
            return value.Length == 0 ? null : value;
        }

        return null;
    }
}

/// <summary>
/// MultiMC 清单中的组件声明。
/// </summary>
public sealed class MultiMcComponent
{
    /// <summary>
    /// 组件唯一标识，如 <c>net.minecraft</c>、<c>net.minecraftforge</c>、<c>net.fabricmc.fabric-loader</c>。
    /// </summary>
    public string? Uid { get; init; }

    /// <summary>
    /// 组件版本号。
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 组件的显示名称（可选）。
    /// </summary>
    public string? CachedName { get; init; }

    /// <summary>
    /// 是否标记为易变组件（可选）。
    /// </summary>
    public bool CachedVolatile { get; init; }
}
