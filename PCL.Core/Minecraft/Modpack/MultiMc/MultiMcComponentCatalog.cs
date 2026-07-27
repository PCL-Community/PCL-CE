using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack.MultiMc;

/// <summary>
/// MultiMC / Prism 组件 UID 的语义分类。
/// </summary>
public enum MultiMcComponentRole
{
    /// <summary>Minecraft 本体（<c>net.minecraft</c>）。</summary>
    Game,

    /// <summary>模组加载器，可映射到 <see cref="ModLoaderKind"/>。</summary>
    ModLoader,

    /// <summary>
    /// 由启动器自身管理的组件 —— LWJGL、映射表、Java 运行时。
    /// 这些组件的版本由 Minecraft 版本决定，整合包声明的值应当忽略。
    /// </summary>
    LauncherManaged,

    /// <summary>未知 UID —— 可能是自定义组件，应保留其本地补丁。</summary>
    Unknown
}

/// <summary>
/// MultiMC / Prism 已知组件 UID 的登记表。
/// <para>
/// UID 列表取自 meta.prismlauncher.org 与 meta.multimc.org 的 <c>index.json</c>。
/// </para>
/// </summary>
public static class MultiMcComponentCatalog
{
    /// <summary>Minecraft 本体的组件 UID。</summary>
    public const string GameUid = "net.minecraft";

    private static readonly FrozenDictionary<string, ModLoaderKind> _LoaderUids =
        new Dictionary<string, ModLoaderKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["net.minecraftforge"] = ModLoaderKind.Forge,
            ["net.neoforged"] = ModLoaderKind.NeoForge,
            ["net.fabricmc.fabric-loader"] = ModLoaderKind.Fabric,
            ["org.quiltmc.quilt-loader"] = ModLoaderKind.Quilt,
            ["com.mumfrey.liteloader"] = ModLoaderKind.LiteLoader,
            ["optifine"] = ModLoaderKind.OptiFine
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> _LauncherManagedUids =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // LWJGL —— 版本由 Minecraft 版本 JSON 决定
            "org.lwjgl",
            "org.lwjgl3",
            // 映射表 —— 随 Fabric / Quilt 加载器一并安装
            "net.fabricmc.intermediary",
            "org.quiltmc.hashed",
            // Java 运行时 —— 由启动器的 Java 管理负责
            "net.minecraft.java",
            "net.adoptium.java",
            "com.azul.java",
            "com.ibm.java"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>判定组件 UID 的语义分类。</summary>
    public static MultiMcComponentRole GetRole(string? uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return MultiMcComponentRole.Unknown;
        if (string.Equals(uid, GameUid, StringComparison.OrdinalIgnoreCase)) return MultiMcComponentRole.Game;
        if (_LoaderUids.ContainsKey(uid)) return MultiMcComponentRole.ModLoader;
        if (_LauncherManagedUids.Contains(uid)) return MultiMcComponentRole.LauncherManaged;
        return MultiMcComponentRole.Unknown;
    }

    /// <summary>
    /// 将组件 UID 与版本号解析为加载器。
    /// <para>
    /// Forge 的 UID 同时用于 Cleanroom —— Cleanroom 沿用 <c>net.minecraftforge</c>，
    /// 以版本号形如 <c>0.x</c> 区分，这一约定与 PCL 对 Cleanroom 的版本命名一致。
    /// </para>
    /// </summary>
    /// <returns>不是已知加载器时返回 <c>null</c>。</returns>
    public static ModLoaderKind? ResolveLoader(string? uid, string? version)
    {
        if (uid is null || !_LoaderUids.TryGetValue(uid, out var kind)) return null;

        if (kind == ModLoaderKind.Forge && version is not null && version.StartsWith("0.", StringComparison.Ordinal))
            return ModLoaderKind.Cleanroom;

        return kind;
    }
}
