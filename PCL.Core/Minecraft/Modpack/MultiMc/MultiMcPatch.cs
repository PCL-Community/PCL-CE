using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace PCL.Core.Minecraft.Modpack.MultiMc;

/// <summary>
/// 一个 MultiMC JSON Patch。
/// <para>
/// 补丁格式是官方版本 JSON 的变体，字段集合随 MultiMC 版本演进且没有完整文档，
/// 因此这里保留原始 <see cref="JsonObject"/>，只为稳定字段提供强类型访问，
/// 未知字段交由 <see cref="MultiMcPatchMerger"/> 按前缀规则处理。
/// </para>
/// </summary>
public sealed class MultiMcPatch
{
    /// <summary>补丁的原始 JSON。</summary>
    public JsonObject Raw { get; }

    /// <summary>组件 UID。</summary>
    public string Uid { get; }

    /// <summary>组件版本号。</summary>
    public string? Version { get; }

    /// <summary>补丁来源，仅用于日志与诊断。</summary>
    public MultiMcPatchSource Source { get; }

    /// <summary>
    /// 已废弃的排序权重，数值小者先应用。
    /// 仅在无法按 <c>mmc-pack.json</c> 的组件顺序排序时作为回退。
    /// </summary>
    public int Order => Raw["order"]?.GetValue<int?>() ?? 0;

    private MultiMcPatch(JsonObject raw, string uid, string? version, MultiMcPatchSource source)
    {
        Raw = raw;
        Uid = uid;
        Version = version;
        Source = source;
    }

    /// <summary>
    /// 从 JSON 节点构造补丁。
    /// </summary>
    /// <param name="node">补丁 JSON。</param>
    /// <param name="source">补丁来源。</param>
    /// <param name="fallbackUid">
    /// 当补丁自身未声明 <c>uid</c> 时使用的回退值（通常取自文件名）。
    /// </param>
    /// <returns>节点不是对象、或无法确定 UID 时返回 <c>null</c>。</returns>
    public static MultiMcPatch? TryCreate(JsonNode? node, MultiMcPatchSource source, string? fallbackUid = null)
    {
        if (node is not JsonObject raw) return null;

        var uid = raw["uid"]?.GetValue<string?>();
        if (string.IsNullOrWhiteSpace(uid)) uid = fallbackUid;
        if (string.IsNullOrWhiteSpace(uid)) return null;

        return new MultiMcPatch(raw, uid.Trim(), raw["version"]?.GetValue<string?>(), source);
    }

    /// <summary>读取补丁声明的依赖。</summary>
    public IReadOnlyList<MultiMcPatchRequirement> GetRequirements()
    {
        if (Raw["requires"] is not JsonArray requires) return [];

        return requires
            .OfType<JsonObject>()
            .Select(item => new MultiMcPatchRequirement(
                item["uid"]?.GetValue<string?>(),
                item["equals"]?.GetValue<string?>(),
                item["suggests"]?.GetValue<string?>()))
            .Where(requirement => !string.IsNullOrWhiteSpace(requirement.Uid))
            .ToArray();
    }
}

/// <summary>
/// 补丁的来源。
/// </summary>
public enum MultiMcPatchSource
{
    /// <summary>来自压缩包内的 <c>patches/</c> 目录。</summary>
    Local,

    /// <summary>来自 MultiMC / Prism 元数据 API。</summary>
    Remote
}

/// <summary>
/// 补丁声明的一项依赖。
/// </summary>
/// <param name="Uid">被依赖组件的 UID。</param>
/// <param name="RequiredVersion">要求的精确版本（补丁中的 <c>equals</c> 字段）。</param>
/// <param name="SuggestedVersion">建议的版本（补丁中的 <c>suggests</c> 字段）。</param>
public readonly record struct MultiMcPatchRequirement(string? Uid, string? RequiredVersion, string? SuggestedVersion)
{
    /// <summary>推荐采用的版本 —— 优先精确要求，其次建议值。</summary>
    public string? PreferredVersion
        => !string.IsNullOrWhiteSpace(RequiredVersion) ? RequiredVersion : SuggestedVersion;
}
