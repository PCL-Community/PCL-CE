using System.Collections.Generic;
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
    /// <summary>合并结果是否为空 —— 为空时无需对实例 JSON 做任何处理。</summary>
    public bool IsEmpty => VersionJson.Count == 0;

    /// <summary>
    /// 补丁是否自带完整的游戏启动参数。
    /// <para>
    /// 为 <c>true</c> 时，实例 JSON 中旧式的 <c>minecraftArguments</c> 字段应被移除 ——
    /// 该字段与 <c>arguments.game</c> 表达同一件事，同时存在会让参数被传入两遍。
    /// </para>
    /// </summary>
    public bool OverridesGameArguments => VersionJson["arguments"]?["game"] is JsonArray { Count: > 0 };
}
