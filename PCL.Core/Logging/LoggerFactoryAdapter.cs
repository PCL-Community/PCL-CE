using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PCL.Core.Logging;

/// <summary>
/// ILoggerFactory 实现，用于创建 LoggerAdapter 实例
/// </summary>
public class LoggerFactoryAdapter : ILoggerFactory
{
    private readonly Logger _logger;
    private readonly List<IDisposable> _disposables = new();

    public LoggerFactoryAdapter(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void AddProvider(ILoggerProvider provider)
    {
        // 不需要实现，因为我们只有一个固定的 Logger
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new LoggerAdapter(_logger, categoryName);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
        _disposables.Clear();
    }
}