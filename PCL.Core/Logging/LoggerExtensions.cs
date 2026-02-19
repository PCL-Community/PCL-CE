using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace PCL.Core.Logging;

public static class LoggerExtensions
{
    /// <summary>
    /// 创建 ILogger 实例
    /// </summary>
    /// <param name="logger">现有的 Logger 实例</param>
    /// <param name="categoryName">日志类别名称</param>
    /// <returns>ILogger 实例</returns>
    public static ILogger CreateLogger(this Logger logger, string categoryName)
    {
        return new LoggerAdapter(logger, categoryName);
    }

    /// <summary>
    /// 创建 ILogger 工厂
    /// </summary>
    /// <param name="logger">现有的 Logger 实例</param>
    /// <returns>ILoggerFactory 实例</returns>
    public static ILoggerFactory CreateLoggerFactory(this Logger logger)
    {
        return new LoggerFactoryAdapter(logger);
    }

    /// <summary>
    /// 使用结构化日志记录的扩展方法
    /// </summary>
    public static void LogInformation<T0>(this ILogger logger, string message, T0 arg0)
    {
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, message, arg0);
    }

    public static void LogInformation<T0, T1>(this ILogger logger, string message, T0 arg0, T1 arg1)
    {
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, message, arg0, arg1);
    }

    public static void LogInformation<T0, T1, T2>(this ILogger logger, string message, T0 arg0, T1 arg1, T2 arg2)
    {
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Information, message, arg0, arg1, arg2);
    }

    public static void LogWarning<T0>(this ILogger logger, string message, T0 arg0)
    {
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Warning, message, arg0);
    }

    public static void LogWarning<T0, T1>(this ILogger logger, string message, T0 arg0, T1 arg1)
    {
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Warning, message, arg0, arg1);
    }

    public static void LogError<T0>(this ILogger logger, Exception? exception, string message, T0 arg0)
    {
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, exception, message, arg0);
    }

    public static void LogError<T0, T1>(this ILogger logger, Exception? exception, string message, T0 arg0, T1 arg1)
    {
        logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, exception, message, arg0, arg1);
    }

    /// <summary>
    /// 条件日志记录扩展方法
    /// </summary>
    public static void LogIf(this ILogger logger, bool condition, Microsoft.Extensions.Logging.LogLevel level, string message)
    {
        if (condition)
        {
            logger.Log(level, message);
        }
    }

    public static void LogIf(this ILogger logger, bool condition, Microsoft.Extensions.Logging.LogLevel level, Exception? exception, string message)
    {
        if (condition)
        {
            logger.Log(level, exception, message);
        }
    }

    /// <summary>
    /// 性能计时日志记录
    /// </summary>
    public static IDisposable LogPerformance(this ILogger logger, string operationName)
    {
        var startTime = DateTime.UtcNow;
        logger.LogInformation("开始执行: {OperationName}", operationName);
        
        return new PerformanceLoggerDisposable(logger, operationName, startTime);
    }

    private class PerformanceLoggerDisposable : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly long _startTime;

        public PerformanceLoggerDisposable(ILogger logger, string operationName, DateTime startTime)
        {
            _logger = logger;
            _operationName = operationName;
            _startTime = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_startTime);
            _logger.LogInformation("完成执行: {OperationName}, 耗时: {ElapsedMs}ms", _operationName, elapsed.TotalMilliseconds);
        }
    }
}