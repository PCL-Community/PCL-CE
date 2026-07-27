using System.Text.Json.Serialization;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack.Manifests;

/// <summary>
/// MCBBS 与 Server 格式共用的组件条目。
/// </summary>
public sealed class ModpackAddon
{
    /// <summary>组件标识，取值见 <see cref="ModpackAddonCatalog"/>。</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

/// <summary>
/// <c>addons</c> 组件标识与加载器种类的映射。
/// <para>
/// 这套标识沿用 HMCL 的 patch id 命名：Minecraft 本体记作 <c>game</c> 而非 <c>minecraft</c>。
/// 部分第三方文档误记为 <c>minecraft</c>，故此处一并接受该别名。
/// </para>
/// </summary>
public static class ModpackAddonCatalog
{
    /// <summary>Minecraft 本体的组件标识。</summary>
    public const string GameId = "game";

    /// <summary>判断组件标识是否指代 Minecraft 本体。</summary>
    public static bool IsGame(string? id) => id?.ToLowerInvariant() is GameId or "minecraft";

    /// <summary>
    /// 将组件标识解析为加载器种类，无法识别时返回 <c>null</c>。
    /// </summary>
    public static ModLoaderKind? ResolveLoader(string? id) => id?.ToLowerInvariant() switch
    {
        "forge" => ModLoaderKind.Forge,
        "neoforge" or "neo-forge" => ModLoaderKind.NeoForge,
        "fabric" => ModLoaderKind.Fabric,
        "legacyfabric" or "legacy-fabric" => ModLoaderKind.LegacyFabric,
        "quilt" => ModLoaderKind.Quilt,
        "liteloader" => ModLoaderKind.LiteLoader,
        "optifine" => ModLoaderKind.OptiFine,
        "cleanroom" => ModLoaderKind.Cleanroom,
        _ => null
    };
}
