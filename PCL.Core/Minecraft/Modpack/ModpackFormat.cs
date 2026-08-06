namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包压缩包的可识别格式。
/// </summary>
public enum ModpackFormat
{
    /// <summary>
    /// 未能识别出任何已知格式。
    /// </summary>
    Unknown,

    /// <summary>
    /// CurseForge 格式，清单文件为 <c>manifest.json</c>（不含 <c>addons</c> 字段）。
    /// </summary>
    CurseForge,

    /// <summary>
    /// Modrinth 格式，清单文件为 <c>modrinth.index.json</c>。
    /// </summary>
    Modrinth,

    /// <summary>
    /// MultiMC 格式，含 <c>mmc-pack.json</c> 与 <c>instance.cfg</c>。
    /// </summary>
    MultiMc,

    /// <summary>
    /// MCBBS 格式，清单文件为 <c>mcbbs.packmeta</c>，或带 <c>addons</c> 字段的 <c>manifest.json</c>。
    /// </summary>
    Mcbbs,

    /// <summary>
    /// HMCL 格式，清单文件为 <c>modpack.json</c>。
    /// </summary>
    Hmcl,

    /// <summary>
    /// 带启动器或嵌套整合包的压缩包：内含 <c>modpack.zip</c> / <c>modpack.mrpack</c>，
    /// 或一层子目录内包含任意格式的整合包。
    /// </summary>
    LauncherPack,

    /// <summary>
    /// 懒人包：内含 <c>.minecraft/versions/&lt;版本&gt;/&lt;版本&gt;.json</c> 完整实例目录的压缩包。
    /// </summary>
    LazyPack
}
