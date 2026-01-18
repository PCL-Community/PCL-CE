using System;
using System.Collections.Generic;

namespace PCL.Core.Minecraft.Instance.Impl;

/// <summary>
/// 表示 Minecraft 实例中附加组件（如 OptiFine、Forge 等）的版本信息和安装状态。
/// </summary>
public class PatchInfo {
    /// <summary>
    /// 附加组件的 identifier，例如 "optiFine" 或 "forge"。
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 附加组件的名称，例如 "OptiFine" 或 "Forge"。
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// 附加组件的版本号，例如 "C8"（OptiFine）或 "31.1.2"（Forge）。
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// 加载的优先级
    /// </summary>
    public int? Priority { get; set; }

    /// <summary>
    /// 覆盖性的游戏启动参数
    /// </summary>
    public string? MinecraftArguments { get; set; }

    /// <summary>
    /// 启动的附加参数
    /// </summary>
    public Dictionary<string, List<string>>? Arguments { get; set; }

    /// <summary>
    /// 附加组件所需的库列表
    /// </summary>
    public List<Dictionary<string, object>>? Libraries { get; set; }

    /// <summary>
    /// Java 版本
    /// </summary>
    public Dictionary<string, object>? JavaVersion { get; set; }

    /// <summary>
    /// 主类名称
    /// </summary>
    public string? MainClass { get; set; }

    /// <summary>
    /// downloads
    /// </summary>
    public Dictionary<string, object>? Downloads { get; set; }

    /// <summary>
    /// logging
    /// </summary>
    public Dictionary<string, object>? Logging { get; set; }

    /// <summary>
    /// inheritsFrom
    /// </summary>
    public string? InheritsFrom { get; set; }

    /// <summary>
    /// releaseTime
    /// </summary>
    public DateTime? ReleaseTime { get; set; }
}
