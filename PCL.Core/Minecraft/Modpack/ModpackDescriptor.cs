using System.Collections.Generic;
using System.Text.Json.Nodes;
using PCL.Core.Minecraft.Modpack.Model;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包的归一化描述 —— 各格式解析后的统一产物。
/// <para>
/// 这是识别层与安装层之间唯一的契约：Provider 负责把千差万别的清单映射到本类型，
/// 之后的规划、下载、落盘都只面向本类型，不再关心原始格式。
/// 本类型不含任何 I/O 与副作用，可直接在单元测试中断言。
/// </para>
/// </summary>
public sealed class ModpackDescriptor
{
    /// <summary>识别到的格式。</summary>
    public required ModpackFormat Format { get; init; }

    /// <summary>展示信息。</summary>
    public ModpackMetadata Metadata { get; init; } = ModpackMetadata.Empty;

    /// <summary>游戏版本与加载器。</summary>
    public required ModpackComponents Components { get; init; }

    /// <summary>覆写目录指令，按应用顺序排列 —— 后者覆盖前者。</summary>
    public IReadOnlyList<ModpackOverride> Overrides { get; init; } = [];

    /// <summary>需要下载的文件。</summary>
    public IReadOnlyList<ModpackFile> Files { get; init; } = [];

    /// <summary>实例启动设置。</summary>
    public ModpackLaunchOptions LaunchOptions { get; init; } = ModpackLaunchOptions.None;

    /// <summary>需要叠加到实例 JSON 的版本补丁，无补丁时为 <c>null</c>。</summary>
    public ModpackVersionPatch? VersionPatch { get; init; }

    /// <summary>需要特殊处理的内嵌目录（库文件、JAR Mod）。</summary>
    public IReadOnlyList<ModpackEmbeddedPayload> EmbeddedPayloads { get; init; } = [];

    /// <summary>
    /// 原始清单快照，写入实例的 <c>modpack.json</c> 以支持后续更新比对。
    /// </summary>
    public JsonNode? RawManifest { get; init; }

    /// <summary>
    /// 解析过程中产生的非致命问题，例如被跳过的畸形条目。
    /// <para>
    /// 数量与整合包的条目数同阶，因此<b>不要逐条提示用户</b>：调用方若要提示，
    /// 应先汇总成一条。同理，实现方不得用 <c>LogWrapper.Warn</c> 逐条记录 ——
    /// 调试构建中该等级会弹出提示条。
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
