using System;

namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 存档信息数据模型
/// </summary>
public class LevelDataInfo
{
    /// <summary>存档名称</summary>
    public string LevelName { get; set; } = "未知";
    
    /// <summary>版本名称 (如 "1.20.4")</summary>
    public string? VersionName { get; set; }
    
    /// <summary>版本ID (数据版本)</summary>
    public int? VersionId { get; set; }
    
    /// <summary>世界种子</summary>
    public string Seed { get; set; } = "获取失败";
    
    /// <summary>是否有命令权限标签</summary>
    public bool HasAllowCommands { get; set; }
    
    /// <summary>命令权限值 (0=不允许, 1=允许)</summary>
    public int? AllowCommands { get; set; }
    
    /// <summary>是否有难度标签</summary>
    public bool HasDifficulty { get; set; }
    
    /// <summary>26.1+ 难度字符串 (peaceful/easy/normal/hard)</summary>
    public string? Difficulty { get; set; }
    
    /// <summary>26.1前 难度数字 (0-3)</summary>
    public int? DifficultyOld { get; set; }
    
    /// <summary>难度是否锁定</summary>
    public bool IsDifficultyLocked { get; set; }
    
    /// <summary>是否为极限模式</summary>
    public bool IsHardcore { get; set; }
    
    /// <summary>最后游玩时间</summary>
    public DateTime LastPlayed { get; set; }
    
    /// <summary>出生点坐标</summary>
    public string SpawnPoint { get; set; } = "获取失败";
    
    /// <summary>游戏模式名称</summary>
    public string GameType { get; set; } = "生存模式";
    
    /// <summary>游戏时长</summary>
    public TimeSpan PlayTime { get; set; }
    
    /// <summary>数据版本号 (可为null表示不存在)</summary>
    public int? DataVersion { get; set; }
    
    /// <summary>是否有DataVersion标签</summary>
    public bool HasDataVersion { get; set; }
    
    /// <summary>是否为26.1+新格式</summary>
    public bool IsNewFormat => HasDataVersion && DataVersion >= 4774;
    
    /// <summary>获取难度显示名称</summary>
    public string DifficultyDisplayName
    {
        get
        {
            if (IsNewFormat && Difficulty != null)
                return Difficulty switch
                {
                    "peaceful" => "和平",
                    "easy" => "简单",
                    "normal" => "普通",
                    "hard" => "困难",
                    _ => "未知"
                };
            
            return DifficultyOld switch
            {
                0 => "和平",
                1 => "简单",
                2 => "普通",
                3 => "困难",
                _ => "未知"
            };
        }
    }
}