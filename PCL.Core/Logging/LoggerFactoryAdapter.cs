using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.Logging;

/// <summary>
/// Minimal <see cref="ILoggerFactory" /> that hands out <see cref="LoggerAdapter" /> instances bound to a
/// single <see cref="Logger" />. Providers are accepted for API compatibility only and are never invoked;
/// they are disposed together with the factory.
/// </summary>
public class LoggerFactoryAdapter(Logger logger) : ILoggerFactory
{
    private readonly Logger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly List<IDisposable> _disposables = [];
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Stores the provider so it can be disposed later. Providers do not participate in logging because
    /// this factory always writes through a single <see cref="Logger" />. Adding after disposal is ignored.
    /// </summary>
    public void AddProvider(ILoggerProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock)
        {
            if (_disposed) return;
            _disposables.Add(provider);
        }
    }

    /// <summary>Creates an adapter for the given category. The category is recorded as the log entry source.</summary>
    public ILogger CreateLogger(string categoryName)
    {
        return new LoggerAdapter(_logger, categoryName);
    }

    /// <summary>Disposes every stored provider exactly once. Safe to call concurrently and repeatedly.</summary>
    public void Dispose()
    {
        IDisposable[] disposables;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            disposables = _disposables.ToArray();
            _disposables.Clear();
        }

        foreach (var disposable in disposables) disposable.Dispose();
    }
}
