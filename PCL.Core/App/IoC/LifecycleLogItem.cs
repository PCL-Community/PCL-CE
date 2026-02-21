using System;
using System.Threading;
using PCL.Core.Logging;

namespace PCL.Core.App.IoC;

/// <summary>
/// 生命周期日志项
/// </summary>
/// <param name="Source">日志来源</param>
/// <param name="Message">日志内容</param>
/// <param name="Exception">相关异常</param>
/// <param name="Level">日志等级</param>
/// <param name="ActionLevel">行为等级</param>
public record LifecycleLogItem(
    ILifecycleService? Source,
    string Message,
    Exception? Exception,
    LogLevel Level,
    ActionLevel ActionLevel)
{
    /// <summary>
    /// 创建该日志项的时间
    /// </summary>
    public DateTime Time { get; } = DateTime.Now;

    /// <summary>
    /// 创建该日志项的线程名
    /// </summary>
    public string ThreadName { get; } = Thread.CurrentThread.Name ?? $"#{Environment.CurrentManagedThreadId}";

    public override string ToString()
    {
        var source = (Source == null) ? "" : $" [{Source.Name}|{Source.Identifier}]";
        var basic = $"[{Time:HH:mm:ss.fff}]{source}";
        return Exception == null ? $"{basic} {Message}" : $"{basic} ({Message}) {Exception.GetType().FullName}: {Exception.Message}";
    }

    public string ComposeMessage()
    {
        var source = (Source == null) ? "" : $" [{Source.Name}|{Source.Identifier}]";
        var result = $"[{Time:HH:mm:ss.fff}] [{Level.RealLevel().PrintName()}] [{ThreadName}]{source} {Message}";
        if (Exception != null) result += $"\n{Exception}";
        return result;
    }
}
