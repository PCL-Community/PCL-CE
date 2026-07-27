using System;
using System.IO;

namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 资源种类与实例目录布局的对应关系。
/// </summary>
public static class ModpackResourcePaths
{
    /// <summary>模组目录。</summary>
    public const string ModsDirectory = "mods";

    /// <summary>资源包目录。</summary>
    public const string ResourcePacksDirectory = "resourcepacks";

    /// <summary>光影包目录。</summary>
    public const string ShaderPacksDirectory = "shaderpacks";

    /// <summary>存档目录。</summary>
    public const string SavesDirectory = "saves";

    /// <summary>
    /// 返回资源种类默认的落地目录。
    /// <para>
    /// 数据包归入资源包目录：数据包本应放在具体存档的 <c>datapacks</c> 下，
    /// 但整合包清单不提供存档上下文，此处与 HMCL 的处理保持一致。
    /// </para>
    /// </summary>
    public static string GetDirectory(ModpackResourceKind kind) => kind switch
    {
        ModpackResourceKind.ResourcePack or ModpackResourceKind.DataPack => ResourcePacksDirectory,
        ModpackResourceKind.ShaderPack => ShaderPacksDirectory,
        ModpackResourceKind.World => SavesDirectory,
        _ => ModsDirectory
    };

    /// <summary>
    /// 由目标路径反推资源种类，用于清单已给出路径、但需要按种类过滤的场景。
    /// </summary>
    public static ModpackResourceKind InferKind(string relativePath)
    {
        var separator = relativePath.IndexOfAny([Path.DirectorySeparatorChar, '/']);
        if (separator <= 0) return ModpackResourceKind.Unknown;

        return relativePath[..separator].ToLowerInvariant() switch
        {
            ModsDirectory => ModpackResourceKind.Mod,
            ResourcePacksDirectory => ModpackResourceKind.ResourcePack,
            ShaderPacksDirectory => ModpackResourceKind.ShaderPack,
            SavesDirectory => ModpackResourceKind.World,
            _ => ModpackResourceKind.Unknown
        };
    }

    /// <summary>
    /// 拼接资源种类默认目录下的相对路径。
    /// </summary>
    /// <exception cref="ModpackUnsafePathException">文件名不合法。</exception>
    public static string CombineWithDirectory(ModpackResourceKind kind, string fileName)
    {
        if (fileName.IndexOfAny(['/', '\\']) >= 0 || fileName is "." or "..")
            throw new ModpackUnsafePathException(fileName);

        var combined = $"{GetDirectory(kind)}{Path.DirectorySeparatorChar}{fileName}";
        if (!ModpackPathPolicy.TryNormalizeRelativePath(combined, out var normalized))
            throw new ModpackUnsafePathException(fileName);

        return normalized;
    }
}
