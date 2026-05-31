using System;
using System.Numerics;

namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 存档核心数据模型 —— 不可变记录，由解析器从 level.dat 中提取。
/// 调用方通过 <see cref="SaveManager"/> 获取此对象。
/// </summary>
public sealed record SaveInfo
{
    /// <summary>世界名称。</summary>
    public required string LevelName { get; init; }

    /// <summary>最后保存此存档的游戏版本名（如 "1.20.4"）。</summary>
    public string? VersionName { get; init; }

    /// <summary>最后保存此存档的游戏数据版本号（对应 <c>Data.Version.Id</c>）。</summary>
    public int? VersionId { get; init; }

    /// <summary>世界种子。</summary>
    public long? Seed { get; init; }

    /// <summary>最后游玩时间（UTC）。</summary>
    public DateTime LastPlayedUtc { get; init; }

    /// <summary>出生点坐标 (X, Y, Z)。</summary>
    public Vector3? Spawn { get; init; }

    /// <summary>游戏模式。Hardcore 通过 IsHardcore 字段表示。</summary>
    public GameMode GameMode { get; init; }

    /// <summary>游戏难度。1.3.1 之前的存档中可能为 null。</summary>
    public Difficulty? Difficulty { get; init; }

    /// <summary>难度是否已锁定。</summary>
    public bool IsDifficultyLocked { get; init; }

    /// <summary>是否为极限模式。</summary>
    public bool IsHardcore { get; init; }

    /// <summary>是否允许作弊命令。</summary>
    public bool AllowCommands { get; init; }

    /// <summary>累计游戏时间。</summary>
    public TimeSpan PlayTime { get; init; }

    /// <summary>存档文件夹的绝对路径。</summary>
    public required string FolderPath { get; init; }

    /// <summary>存档文件夹的创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>level.dat 的最后修改时间（UTC）。</summary>
    public DateTime ModifiedAt { get; init; }
}
