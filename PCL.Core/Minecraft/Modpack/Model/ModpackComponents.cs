using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 模组加载器种类。
/// <para>
/// 这里列出的是「能够识别」的加载器，与「能够安装」是两件事：
/// 识别层保持完整，安装能力由 <see cref="ModpackLoaderSupport"/> 单独判定，
/// 这样遇到不支持的加载器时可以给出精确的原因而不是笼统的解析失败。
/// </para>
/// </summary>
public enum ModLoaderKind
{
    Forge,
    NeoForge,
    Fabric,
    LegacyFabric,
    Quilt,
    LiteLoader,
    OptiFine,
    Cleanroom
}

/// <summary>
/// 一个加载器及其版本。
/// </summary>
/// <param name="Kind">加载器种类。</param>
/// <param name="Version">加载器版本号，原样保留清单中的写法。</param>
/// <param name="IsPrimary">是否为整合包声明的主加载器。</param>
public sealed record ModpackLoader(ModLoaderKind Kind, string Version, bool IsPrimary = false);

/// <summary>
/// 当前启动器对各加载器的安装支持情况。
/// </summary>
public static class ModpackLoaderSupport
{
    /// <summary>PCL CE 已不再支持 Quilt，其余加载器均可安装。</summary>
    public static bool IsInstallable(ModLoaderKind kind) => kind is not ModLoaderKind.Quilt;

    /// <summary>返回加载器不可安装的原因，可安装时返回 <c>null</c>。</summary>
    public static string? GetUnsupportedReason(ModLoaderKind kind) => kind switch
    {
        ModLoaderKind.Quilt => "当前启动器已不再支持 Quilt 加载器",
        _ => null
    };
}

/// <summary>
/// 整合包声明的游戏组件集合 —— 一个 Minecraft 版本，以及零到多个加载器。
/// </summary>
public sealed class ModpackComponents
{
    /// <summary>Minecraft 版本号。</summary>
    public string GameVersion { get; }

    /// <summary>加载器列表，已按解析顺序去重。</summary>
    public IReadOnlyList<ModpackLoader> Loaders { get; }

    public ModpackComponents(string gameVersion, IEnumerable<ModpackLoader>? loaders = null)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
            throw new ArgumentException("Minecraft 版本号不能为空", nameof(gameVersion));

        GameVersion = gameVersion.Trim();

        // 同一种加载器出现多次时保留首个，避免清单重复声明导致后续安装请求冲突
        Loaders = loaders?
            .Where(loader => !string.IsNullOrWhiteSpace(loader.Version))
            .GroupBy(loader => loader.Kind)
            .Select(group => group.First())
            .ToArray() ?? [];
    }

    /// <summary>查找指定种类的加载器版本，不存在时返回 <c>null</c>。</summary>
    public string? GetLoaderVersion(ModLoaderKind kind)
        => Loaders.FirstOrDefault(loader => loader.Kind == kind)?.Version;

    /// <summary>
    /// 检查全部加载器均可安装，否则抛出 <see cref="ModpackUnsupportedContentException"/>。
    /// </summary>
    /// <exception cref="ModpackUnsupportedContentException" />
    public void EnsureInstallable()
    {
        foreach (var loader in Loaders)
        {
            var reason = ModpackLoaderSupport.GetUnsupportedReason(loader.Kind);
            if (reason is not null) throw new ModpackUnsupportedContentException($"无法安装整合包：{reason}。");
        }
    }
}
