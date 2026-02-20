using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace PCL.Core.Logging;

/// <summary>
/// Microsoft.Extensions.Logging.ILogger 适配器
/// 将现有的 Logger 包装为标准的 ILogger 接口实现
/// </summary>
public class LoggerAdapter(Logger logger, string categoryName) : ILogger
{
    private readonly Logger _innerLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));

    private static readonly AsyncLocal<Stack<object>> _ScopeStack = new();

    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        _ScopeStack.Value ??= new Stack<object>();
        _ScopeStack.Value.Push(state);
        return new ScopeDisposable(state);
    }

#pragma warning disable CS9113 // 参数未读。
    private class ScopeDisposable(object state) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            if (_ScopeStack.Value is { Count: > 0 })
            {
#if DEBUG
                var popped = _ScopeStack.Value.Pop();
                if (!ReferenceEquals(popped, state))
                {
                    throw new InvalidOperationException("Scope disposal order mismatch.");
                }
#else
                _ = _ScopeStack.Value.Pop();
#endif
            }

            _disposed = true;
        }
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        ArgumentNullException.ThrowIfNull(formatter);

        var originalMessage = formatter(state, exception);
        var sb = new StringBuilder();

        // 类别名称
        if (!string.IsNullOrEmpty(_categoryName))
        {
            sb.Append('[').Append(_categoryName).Append("] ");
        }

        // 事件 ID
        if (eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
        {
            sb.Append("[EventId:");
            if (!string.IsNullOrEmpty(eventId.Name))
            {
                sb.Append(eventId.Id).Append(':').Append(eventId.Name);
            }
            else
            {
                sb.Append(eventId.Id);
            }
            sb.Append("] ");
        }

        // 上下文信息
        var scopeContext = _BuildScopeContext();
        if (!string.IsNullOrEmpty(scopeContext))
        {
            sb.Append('[').Append(_categoryName).Append("] ");
        }

        sb.Append(originalMessage);
        var finalMessage = sb.ToString();

        // 日志级别
        switch (logLevel)
        {
            case Microsoft.Extensions.Logging.LogLevel.Trace:
                _innerLogger.Trace(finalMessage);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Debug:
                _innerLogger.Debug(finalMessage);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Information:
                _innerLogger.Info(finalMessage);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Warning:
                _innerLogger.Warn(finalMessage);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Error:
                _innerLogger.Error(finalMessage);
                break;
            case Microsoft.Extensions.Logging.LogLevel.Critical:
                _innerLogger.Fatal(finalMessage);
                break;
        }

        if (exception != null)
        {
            var exceptionMessage = $"Exception: {exception}";
            _innerLogger.Log($"[{_categoryName}] {exceptionMessage}");
        }
    }

    private string _BuildScopeContext()
    {
        var stack = _ScopeStack.Value;
        if (stack == null || stack.Count == 0)
            return string.Empty;

        var scopes = stack.AsEnumerable().Reverse();
        return string.Join(" => ", scopes.Select(s => s.ToString()));
    }
}
