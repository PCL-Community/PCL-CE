using Humanizer;

namespace PCL.Core.Utils;

using System;

/// <summary>
/// 提供与时间相关的实用方法。
/// </summary>
public static class TimeUtils {
    /// <summary>
    /// 获取格式类似于“11:08:52.037”的当前时间字符串。
    /// </summary>
    /// <returns>格式化后的时间字符串。</returns>
    public static string GetTimeNow() {
        return DateTime.Now.ToString("HH:mm:ss.fff");
    }

    /// <summary>
    /// 获取系统运行时间（毫秒），保证为正长整型，且大于 1。
    /// </summary>
    /// <remarks>
    /// 此方法基于 Environment.TickCount，但在 .NET 框架中，我们有更可靠的替代方案。
    /// </remarks>
    /// <returns>系统运行毫秒数。</returns>
    public static long GetTimeTick() {
        // 原始代码处理了 Environment.TickCount 的符号溢出问题（在 24.8 天后），
        // 但在现代 .NET 中，我们有更可靠、更精确的 Stopwatch 类。
        // 为了保持原函数意图，这里直接返回 Environment.TickCount。
        // 注意：Environment.TickCount 在64位系统中可能为负，不推荐在重要场景使用。
        return Environment.TickCount64;
    }

    /// <summary>
    /// 将时间间隔转换为易于阅读的形式。
    /// </summary>
    /// <param name="span">要转换的时间间隔。</param>
    /// <param name="isShortForm">如果为 true，则使用简短格式。</param>
    /// <returns>格式化后的时间间隔字符串。</returns>
    public static string GetTimeSpanString(TimeSpan span, bool isShortForm) {
        var isPast = span.TotalMilliseconds < 0;
        var endFix = isPast ? "前" : "后";
        if (isPast) {
            span = span.Negate();
        }

        return span.Humanize(precision: isShortForm ? 1 : 2, maxUnit: TimeUnit.Year, minUnit:TimeUnit.Hour) + endFix;
    }

    /// <summary>
    /// 获取十进制 Unix 时间戳（秒）。
    /// </summary>
    /// <returns>当前时间的 Unix 时间戳。</returns>
    public static long GetUnixTimestamp() {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
    
    /// <summary>
    /// 将 Unix 时间戳（秒）转换为本地时区的日期时间。
    /// </summary>
    /// <param name="unixTimestamp">Unix 时间戳（秒），表示自 1970-01-01 00:00:00 UTC 起的秒数。</param>
    /// <returns>转换后的本地日期时间。</returns>
    /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="unixTimestamp"/> 为负数或过大时抛出。</exception>
    public static DateTimeOffset FromUnixTimestamp(long unixTimestamp)
    {
        if (unixTimestamp < 0)
            throw new ArgumentOutOfRangeException(nameof(unixTimestamp), "Unix 时间戳不能为负数。");

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ArgumentOutOfRangeException(nameof(unixTimestamp), "Unix 时间戳超出有效范围。", ex.Message);
        }
    }

    /// <summary>
    /// 将 UTC 时间转换为当前时区的时间。
    /// </summary>
    /// <param name="utcDate">UTC 日期时间。</param>
    /// <returns>转换后的本地日期时间。</returns>
    public static DateTimeOffset ToLocalTime(DateTimeOffset utcDate) =>
        utcDate.ToLocalTime();
    
    /// <summary>
    /// 将 Unix 时间戳（秒）转换为格式化的本地时间字符串（yyyy/MM/dd HH:mm）。
    /// </summary>
    /// <param name="unixTimestamp">Unix 时间戳（秒），表示自 1970-01-01 00:00:00 UTC 起的秒数。</param>
    /// <returns>格式为 yyyy/MM/dd HH:mm 的本地时间字符串。</returns>
    /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="unixTimestamp"/> 为负数或过大时抛出。</exception>
    public static string FormatUnixTimestamp(long unixTimestamp) =>
        FromUnixTimestamp(unixTimestamp).ToString("yyyy/MM/dd HH:mm");
}
