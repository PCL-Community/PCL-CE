using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Modpack.Model;

/// <summary>
/// 由整合包自带的版本 JSON 补丁合并而来的实例 JSON 片段。
/// <para>
/// 目前只有 MultiMC / Prism 格式会产出：其 <c>patches/</c> 目录与 meta API 返回的组件
/// 都是官方版本 JSON 的变体，合并后需要叠加到启动器生成的实例 JSON 上。
/// </para>
/// </summary>
/// <param name="VersionJson">合并结果，字段名与官方版本 JSON 一致。</param>
/// <param name="ReplacesGameJson">
/// 为 <c>true</c> 时表示整合包自带了 <c>net.minecraft</c> 组件的完整补丁，
/// 应当直接取代启动器下载的原版 JSON，而不是与之合并。
/// </param>
/// <param name="AppliedComponentUids">参与合并的组件 UID，按应用顺序排列，仅用于日志与诊断。</param>
public sealed record ModpackVersionPatch(
    JsonObject VersionJson,
    bool ReplacesGameJson,
    IReadOnlyList<string> AppliedComponentUids)
{
    /// <summary>
    /// MultiMC 原始组件顺序。自定义补丁必须在其声明位置应用，不能提前聚合到 Minecraft
    /// 本体上，否则位于 Forge/Fabric 之后的字段会被加载器 JSON 重新覆盖。
    /// </summary>
    public IReadOnlyList<ModpackVersionComponent> OrderedComponents { get; init; } = [];

    /// <summary>JAR Mod 文件名，按组件和补丁中的声明顺序排列。</summary>
    public IReadOnlyList<string> JarModFileNames { get; init; } = [];

    /// <summary>合并结果是否为空 —— 为空时无需对实例 JSON 做任何处理。</summary>
    public bool IsEmpty => VersionJson.Count == 0 &&
                           !OrderedComponents.Any(component =>
                               component.Kind == ModpackVersionComponentKind.CustomPatch);

    /// <summary>
    /// 补丁是否自带完整的游戏启动参数。
    /// <para>
    /// 为 <c>true</c> 时，实例 JSON 中旧式的 <c>minecraftArguments</c> 字段应被移除 ——
    /// 该字段与 <c>arguments.game</c> 表达同一件事，同时存在会让参数被传入两遍。
    /// </para>
    /// </summary>
    public bool OverridesGameArguments =>
        VersionJson["arguments"]?["game"] is JsonArray { Count: > 0 } ||
        OrderedComponents.Any(component => component.Patch is { } patch &&
            (patch["minecraftArguments"] is not null || patch["+gameArgs"] is JsonArray { Count: > 0 }));
}

/// <summary>有序版本组件的种类。</summary>
public enum ModpackVersionComponentKind
{
    /// <summary>Minecraft 本体，由 PCL 下载的原版 JSON 代替。</summary>
    Game,

    /// <summary>PCL 能直接安装的加载器，由对应的安装结果 JSON 代替。</summary>
    Loader,

    /// <summary>启动器无法自行安装的自定义 MultiMC 补丁。</summary>
    CustomPatch
}

/// <summary>
/// MultiMC 组件序列中的一个操作。<see cref="LoaderKind"/> 仅对加载器有效，
/// <see cref="Patch"/> 仅对自定义补丁有效。
/// </summary>
public sealed record ModpackVersionComponent(
    string Uid,
    ModpackVersionComponentKind Kind,
    ModLoaderKind? LoaderKind = null,
    JsonObject? Patch = null);
