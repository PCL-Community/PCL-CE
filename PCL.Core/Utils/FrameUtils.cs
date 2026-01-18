using System;
using System.Diagnostics;

namespace PCL.Core.Utils;

/// <summary>
/// 帮助动画帧的工具类。
/// </summary>
public static class FrameUtils
{
    public static long Frequency { get; }

    static FrameUtils()
    {
        Frequency = Stopwatch.Frequency;
    }
    
    /// <summary>
    /// 获取现在的时间戳。
    /// </summary>
    /// <returns>时间戳。</returns>
    public static long NowStamp() => Stopwatch.GetTimestamp();
    
    /// <summary>
    /// 将时间戳转换为帧索引。
    /// </summary>
    /// <param name="startStamp">开始时的时间戳。</param>
    /// <param name="fps">帧率。</param>
    /// <returns>帧索引。</returns>
    public static long StampToFrameIndex(long startStamp, int fps)
    {
        // 计算经过的时间戳
        var durationStamp = NowStamp() - startStamp;
        if (durationStamp <= 0) return 0;
        
        // 计算帧索引
        var index = durationStamp * fps / Frequency;
        
        return index;
    }
    
    /// <summary>
    /// 将时间跨度转换为帧索引。
    /// </summary>
    /// <param name="startTime">开始时的时间。</param>
    /// <param name="currentTime">当前时间。</param>
    /// <param name="fps">帧率。</param>
    /// <returns>帧索引。</returns>
    public static long TimeSpanToFrameIndex(TimeSpan startTime, TimeSpan currentTime, int fps)
    {
        // 如果当前时间早于开始时间，则返回0
        if (currentTime < startTime) return 0;
        
        // 计算经过的时间
        var duration = currentTime - startTime;
        // 计算帧索引
        var index = (long)(duration.TotalSeconds * fps);

        return index;
    }
}