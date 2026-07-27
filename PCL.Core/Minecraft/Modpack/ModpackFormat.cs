namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 已支持识别与安装的整合包格式。
/// </summary>
public enum ModpackFormat
{
    /// <summary>CurseForge 格式，特征文件 <c>manifest.json</c>（不含 <c>addons</c> 字段）。</summary>
    CurseForge,

    /// <summary>Modrinth 格式（<c>.mrpack</c>），特征文件 <c>modrinth.index.json</c>。</summary>
    Modrinth,

    /// <summary>MultiMC / Prism Launcher 格式，特征文件 <c>mmc-pack.json</c> + <c>instance.cfg</c>。</summary>
    MultiMc,

    /// <summary>MCBBS 格式，特征文件 <c>mcbbs.packmeta</c>，或含 <c>addons</c> 字段的 <c>manifest.json</c>。</summary>
    Mcbbs,

    /// <summary>服务端整合包格式，特征文件 <c>server-manifest.json</c>。</summary>
    Server,

    /// <summary>HMCL 自有格式，特征文件 <c>modpack.json</c>。不属于跨启动器的公共规范。</summary>
    Hmcl
}

/// <summary>
/// <see cref="ModpackFormat"/> 的展示与持久化辅助。
/// </summary>
public static class ModpackFormatExtensions
{
    /// <summary>
    /// 返回格式的展示名称。该名称同时写入实例的 <c>modpack.json</c>，
    /// 取值与 HMCL 的 <c>ModpackConfiguration.type</c> 保持一致，以便双向兼容。
    /// </summary>
    public static string ToDisplayName(this ModpackFormat format) => format switch
    {
        ModpackFormat.CurseForge => "Curse",
        ModpackFormat.Modrinth => "Modrinth",
        ModpackFormat.MultiMc => "MultiMC",
        ModpackFormat.Mcbbs => "Mcbbs",
        ModpackFormat.Server => "Server",
        ModpackFormat.Hmcl => "HMCL",
        _ => format.ToString()
    };

    /// <summary>
    /// 解析 <see cref="ToDisplayName"/> 写出的名称，无法识别时返回 <c>null</c>。
    /// </summary>
    public static ModpackFormat? ParseDisplayName(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "curse" or "curseforge" => ModpackFormat.CurseForge,
        "modrinth" => ModpackFormat.Modrinth,
        "multimc" or "mmc" or "prism" => ModpackFormat.MultiMc,
        "mcbbs" => ModpackFormat.Mcbbs,
        "server" => ModpackFormat.Server,
        "hmcl" => ModpackFormat.Hmcl,
        _ => null
    };
}
