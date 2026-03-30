using System;

namespace PCL.Core.Minecraft.Saves;

public class SavesPlayTime
{
    public static string FormatPlayTime(TimeSpan playTime)
    {
        if (playTime.TotalSeconds < 60)
            return $"{playTime.Seconds} 秒";
        
        if (playTime.TotalHours < 1)
            return $"{playTime.Minutes} 分钟 {playTime.Seconds} 秒";
        
        if (playTime.TotalDays < 1)
            return $"{playTime.Hours} 小时 {playTime.Minutes} 分钟";
        
        return $"{playTime.Days} 天 {playTime.Hours} 小时 {playTime.Minutes} 分钟";
    }
}