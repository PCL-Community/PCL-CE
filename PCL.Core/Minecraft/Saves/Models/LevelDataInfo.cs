using System;

namespace PCL.Core.Minecraft.Saves.Models;

/// <summary>
/// 存档信息数据模型（统一格式，不区分版本）
/// </summary>
public class LevelDataInfo
{
    public string LevelName { get; set; } = null!;   
    public string Seed { get; set; } = null!;    
    public string SpawnPoint { get; set; } = null!;
    public string GameType { get; set; } = null!;
    public string DifficultyDisplay { get; set; } = null!;       
    public DateTime LastPlayed { get; set; }
    public TimeSpan PlayTime { get; set; }
    public string? VersionName { get; set; }
    public int? VersionId { get; set; }
    public bool HasAllowCommands { get; set; }
    public int? AllowCommands { get; set; }
    public bool HasDifficulty { get; set; }
    public bool IsDifficultyLocked { get; set; }
    public bool IsHardcore { get; set; }
    public int? DataVersion { get; set; }
    public bool HasDataVersion { get; set; }
}