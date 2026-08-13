using System.Collections.Generic;
using System.Text.Json.Nodes;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// MCBBS 格式整合包的清单（<c>mcbbs.packmeta</c>）模型。
/// MCBBS 格式可视为 CurseForge 格式的超集：在 <c>manifest.json</c> 的基础上补充了
/// <c>addons</c>（游戏与加载器附加信息）与 <c>launchInfo</c>（启动参数）。
/// 使用 <see cref="JsonCompat"/> 的宽松反序列化读取；缺失的字段保持为 null/空，由安装流程负责校验。
/// </summary>
public sealed class McbbsManifest
{
    /// <summary>
    /// 整合包名称。
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// 整合包版本号。
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// 附加信息列表，键如 <c>game</c>、<c>forge</c>、<c>neoforge</c>、<c>fabric</c>、<c>optifine</c>；
    /// 缺失时表示清单未声明附加信息。
    /// </summary>
    public List<McbbsAddon>? Addons { get; init; }

    /// <summary>
    /// 启动参数信息（可选）。
    /// </summary>
    public McbbsLaunchInfo? LaunchInfo { get; init; }

    /// <summary>
    /// 宽松解析 MCBBS 清单。
    /// </summary>
    /// <param name="node">清单 JSON 节点。</param>
    /// <returns>解析后的清单；传入 null 时返回 null。</returns>
    public static McbbsManifest? Parse(JsonNode? node)
    {
        return node.ToObject<McbbsManifest>();
    }
}

/// <summary>
/// MCBBS 清单中的附加信息项。
/// </summary>
public sealed class McbbsAddon
{
    /// <summary>
    /// 附加信息标识，如 <c>game</c>、<c>forge</c>、<c>fabric</c>。
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// 附加信息版本号。
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// MCBBS 清单中的启动参数信息。
/// </summary>
public sealed class McbbsLaunchInfo
{
    /// <summary>
    /// JVM 参数。保留原始 JSON 节点，由安装流程按既有方式拼接（兼容数组与字符串两种写法）。
    /// </summary>
    public JsonNode? JavaArgument { get; init; }

    /// <summary>
    /// 游戏启动参数。保留原始 JSON 节点，由安装流程按既有方式拼接。
    /// </summary>
    public JsonNode? LaunchArgument { get; init; }
}
