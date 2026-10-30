using Microsoft.Extensions.Logging;
using System;
using System.Text;
using System.Threading;

namespace PCL.Core.Logging;

/// <summary>
/// Adapts <see cref="Logger" /> to <see cref="ILogger" />. The category name becomes the entry source,
/// scopes are rendered as a prefix of the message, and the exception is handed to the logger untouched so
/// its stack trace is rendered exactly once on the writer thread.
/// </summary>
public class LoggerAdapter(Logger logger, string categoryName) : ILogger
{
    private readonly Logger _innerLogger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly string _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));

    /// <summary>An immutable node of the ambient scope chain. Pushing replaces the reference, popping restores the parent.</summary>
    private sealed class ScopeNode(string text, ScopeNode? parent)
    {
        public string Text { get; } = text;
        public ScopeNode? Parent { get; } = parent;
    }

    // Ambient by design: a scope spans categories. An immutable chain is used because a shared mutable
    // stack would let a child async context push onto the parent's stack.
    private static readonly AsyncLocal<ScopeNode?> _Scope = new();

    /// <summary>
    /// Pushes a scope onto the ambient chain, snapshotting <c>state.ToString()</c> once instead of on every
    /// log call, and returns a handle that pops exactly the node it pushed.
    /// </summary>
    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        var node = new ScopeNode(state?.ToString() ?? string.Empty, _Scope.Value);
        _Scope.Value = node;
        return new ScopeDisposable(node);
    }

    /// <summary>
    /// Pops a single scope node. Disposal is idempotent and a no-op when the ambient chain has already moved
    /// on, so out-of-order disposal cannot corrupt the chain.
    /// </summary>
    private sealed class ScopeDisposable(ScopeNode node) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (ReferenceEquals(_Scope.Value, node)) _Scope.Value = node.Parent;
        }
    }

    /// <summary>
    /// Maps the Microsoft level onto the local one and defers to <see cref="Logger.IsEnabled" />, so callers
    /// can skip building the message and the state object entirely.
    /// </summary>
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level)
        => _ToPclLevel(level) is { } pclLevel && _innerLogger.IsEnabled(pclLevel);

    /// <summary>
    /// Formats the message with the exception excluded, adds the event id and scope prefix only when they
    /// exist, and forwards the exception separately. This keeps the exception text in the output exactly
    /// once and avoids allocating a builder for the common case.
    /// </summary>
    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (_ToPclLevel(logLevel) is not { } pclLevel || !_innerLogger.IsEnabled(pclLevel))
            return;

        ArgumentNullException.ThrowIfNull(formatter);

        var message = formatter(state, null);
        var scope = _BuildScopeContext();
        var hasEventId = eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name);

        if (hasEventId || scope is not null)
        {
            var builder = new StringBuilder(message.Length + 48);
            if (hasEventId)
            {
                builder.Append("[EventId:");
                if (!string.IsNullOrEmpty(eventId.Name))
                    builder.Append(eventId.Id).Append(':').Append(eventId.Name);
                else
                    builder.Append(eventId.Id);
                builder.Append("] ");
            }

            if (scope is not null) builder.Append('[').Append(scope).Append("] ");

            builder.Append(message);
            message = builder.ToString();
        }

        _innerLogger.Log(pclLevel, message, _categoryName, exception);
    }

    /// <summary>Renders the ambient scope chain outermost first, joined by <c>=&gt;</c>, or <see langword="null" /> when no scope is active.</summary>
    private static string? _BuildScopeContext()
    {
        var node = _Scope.Value;
        if (node is null) return null;

        var builder = new StringBuilder();
        _AppendScope(builder, node);
        return builder.ToString();
    }

    /// <summary>Appends the chain recursively, so the outermost scope comes first without any reversal buffer.</summary>
    private static void _AppendScope(StringBuilder builder, ScopeNode node)
    {
        if (node.Parent is not null)
        {
            _AppendScope(builder, node.Parent);
            builder.Append(" => ");
        }
        builder.Append(node.Text);
    }

    /// <summary>Maps a Microsoft level onto the local level, or <see langword="null" /> for an unknown value.</summary>
    private static LogLevel? _ToPclLevel(Microsoft.Extensions.Logging.LogLevel level) => level switch
    {
        Microsoft.Extensions.Logging.LogLevel.Trace => LogLevel.Trace,
        Microsoft.Extensions.Logging.LogLevel.Debug => LogLevel.Debug,
        Microsoft.Extensions.Logging.LogLevel.Information => LogLevel.Info,
        Microsoft.Extensions.Logging.LogLevel.Warning => LogLevel.Warning,
        Microsoft.Extensions.Logging.LogLevel.Error => LogLevel.Error,
        Microsoft.Extensions.Logging.LogLevel.Critical => LogLevel.Fatal,
        _ => null
    };
}
