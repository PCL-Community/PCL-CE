namespace PCL.Core.Minecraft.Saves;

/// <summary>
/// 存档版本相关辅助方法
/// </summary>
public static class LevelDataVersion
{
    /// <summary>26.1版本的数据版本</summary>
    public const int DataVersion26_1 = 4774;
    
    /// <summary>1.13版本的数据版本 (数据包功能引入)</summary>
    public const int DataVersion1_13 = 1444;
    
    /// <summary>1.9版本的数据版本</summary>
    public const int DataVersion1_9 = 107;
    
    /// <summary>检查是否应该显示数据包按钮</summary>
    public static bool ShouldShowDataPack(int? dataVersion) 
        => dataVersion.HasValue && dataVersion.Value >= DataVersion1_13;
    
    /// <summary>获取版本提示信息</summary>
    public static string? GetVersionHint(bool hasDataVersion, bool hasDifficulty, bool hasAllowCommands)
    {
        if (hasDataVersion) return null;
        
        if (hasDifficulty)
            return "1.9 以下的版本无法获取存档版本";
        
        if (hasAllowCommands)
            return "1.8 以下的版本无法获取存档版本和游戏难度";
        
        return "1.3 以下的版本无法获取存档版本、游戏难度和是否允许作弊";
    }
}