using System;

namespace PCL.Core.Logging;

public delegate void LogHandler(LogLevel level, string msg, string? module = null, Exception? ex = null);

/// <summary>
/// Static facade used across the launcher to write logs without holding a context. Every call is
/// forwarded to <see cref="OnLog" />, which <see cref="LogService" /> subscribes to at startup; before
/// that subscription exists the calls are silently discarded. Failures inside a handler are swallowed so
/// that logging can never break the caller.
/// </summary>
public static class LogWrapper
{
    public static event LogHandler? OnLog;

    /// <summary>
    /// Invokes the <see cref="OnLog" /> handlers and swallows any exception they throw. The handler is
    /// copied to a local first so an unsubscribing thread cannot turn the invocation into a null
    /// reference.
    /// </summary>
    private static void _Invoke(LogLevel level, string msg, string? module, Exception? ex)
    {
        var handler = OnLog;
        if (handler is null) return;
        try
        {
            handler(level, msg, module, ex);
        }
        catch (Exception)
        {
            // Logging must never take down the caller.
        }
    }

    // Fatal: can handle exceptions
    public static void Fatal(Exception? ex, string? module, string msg) => _Invoke(LogLevel.Fatal, msg, module, ex);
    public static void Fatal(Exception? ex, string msg) => Fatal(ex, null, msg);
    public static void Fatal(string? module, string msg) => Fatal(null, module, msg);
    public static void Fatal(string msg) => Fatal((string?)null, msg);
    
    // Error: can handle exceptions
    public static void Error(Exception? ex, string? module, string msg) => _Invoke(LogLevel.Error, msg, module, ex);
    public static void Error(Exception? ex, string msg) => Error(ex, null, msg);
    public static void Error(string? module, string msg) => Error(null, module, msg);
    public static void Error(string msg) => Error((string?)null, msg);
    
    // Warn: can handle exceptions
    public static void Warn(Exception? ex, string? module, string msg) => _Invoke(LogLevel.Warning, msg, module, ex);
    public static void Warn(Exception? ex, string msg) => Warn(ex, null, msg);
    public static void Warn(string? module, string msg) => Warn(null, module, msg);
    public static void Warn(string msg) => Warn((string?)null, msg);
    
    // Info
    public static void Info(string? module, string msg) => _Invoke(LogLevel.Info, msg, module, null);
    public static void Info(string msg) => Info(null, msg);

    // Debug
    public static void Debug(string? module, string msg) => _Invoke(LogLevel.Debug, msg, module, null);
    public static void Debug(string msg) => Debug(null, msg);
    public static void Debug(Exception ex, string module, string message) => _Invoke(LogLevel.Debug, message, module, ex);

    // Trace
    public static void Trace(string? module, string msg) => _Invoke(LogLevel.Trace, msg, module, null);
    public static void Trace(string msg) => Trace(null, msg);

    /// <summary>The logger owned by <see cref="LogService" />. Throws if accessed before the service started.</summary>
    public static Logger CurrentLogger => LogService.Logger;

    private static readonly Lazy<LoggerFactoryAdapter> _LoggerFactory = new(static () =>
    {
        return new LoggerFactoryAdapter(CurrentLogger);
    });

    /// <summary>Lazily created <c>Microsoft.Extensions.Logging</c> bridge bound to <see cref="CurrentLogger" />.</summary>
    public static LoggerFactoryAdapter LoggerFactory => _LoggerFactory.Value;
}
