namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 整合包内嵌载荷的种类。
/// </summary>
public enum ModpackPayloadKind
{
    /// <summary>内嵌的库文件，需释放到实例的 <c>libraries</c> 目录。</summary>
    Libraries,

    /// <summary>JAR Mod，需在安装完成后合并进 Minecraft 主 JAR。</summary>
    JarMods
}

/// <summary>
/// 整合包内嵌的、需要特殊处理而非简单覆写的目录。
/// </summary>
/// <param name="Kind">载荷种类。</param>
/// <param name="ArchiveDirectory">压缩包内的源目录，相对于逻辑根。</param>
public sealed record ModpackEmbeddedPayload(ModpackPayloadKind Kind, string ArchiveDirectory);
