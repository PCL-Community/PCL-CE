using System;

namespace PCL.Core.Minecraft.Saves.Models;

/// <summary>
/// 存档信息数据模型（统一格式，不区分版本）
/// </summary>
public class LevelDataInfo
{
    public string LevelName { get; set; } = "获取失败";
    public string? VersionName { get; set; }
    public int? VersionId { get; set; }
    public string Seed { get; set; } = "获取失败";
    public bool HasAllowCommands { get; set; }
    public int? AllowCommands { get; set; }
    public bool HasDifficulty { get; set; }
    public string DifficultyDisplay { get; set; } = "获取失败";
    public bool IsDifficultyLocked { get; set; }
    public bool IsHardcore { get; set; }
    public DateTime LastPlayed { get; set; }
    public string SpawnPoint { get; set; } = "获取失败";
    public string GameType { get; set; } = "获取失败";
    public TimeSpan PlayTime { get; set; }
    public int? DataVersion { get; set; }
    public bool HasDataVersion { get; set; }
}