using System;

namespace PCL.Core.Minecraft.Watcher;

/// <summary>游戏会话对应的实例元数据。</summary>
/// <param name="InstanceName">实例名称。</param>
/// <param name="InstanceDescription">实例描述。</param>
/// <param name="ReleaseTime">游戏版本发布时间。</param>
/// <param name="InstanceDirectory">版本实例目录。</param>
/// <param name="GameDirectory">游戏工作目录。</param>
public sealed record GameSessionMetadata(
    string InstanceName,
    string? InstanceDescription,
    DateTime ReleaseTime,
    string? InstanceDirectory,
    string? GameDirectory);