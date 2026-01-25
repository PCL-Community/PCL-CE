using System;

namespace PCL.Core.Logging;

public delegate void LogHandler(LogLevel level, string msg, string? module = null, Exception? ex = null);

public static class LogWrapper
{
    public static event LogHandler? OnLog;
    
    // Fatal: can handle exceptions
    public static void Fatal(Exception? ex, string? module, string msg) => OnLog?.Invoke(LogLevel.Fatal, msg, module, ex);
    public static void Fatal(Exception? ex, string msg) => Fatal(ex, null, msg);
    public static void Fatal(string? module, string msg) => Fatal(null, module, msg);
    public static void Fatal(string msg) => Fatal((string?)null, msg);
    
    // Error: can handle exceptions
    public static void Error(Exception? ex, string? module, string msg) => OnLog?.Invoke(LogLevel.Error, msg, module, ex);
    public static void Error(Exception? ex, string msg) => Error(ex, null, msg);
    public static void Error(string? module, string msg) => Error(null, module, msg);
    public static void Error(string msg) => Error((string?)null, msg);
    
    // Warn: can handle exceptions
    public static void Warn(Exception? ex, string? module, string msg) => OnLog?.Invoke(LogLevel.Warning, msg, module, ex);
    public static void Warn(Exception? ex, string msg) => Warn(ex, null, msg);
    public static void Warn(string? module, string msg) => Warn(null, module, msg);
    public static void Warn(string msg) => Warn((string?)null, msg);
    
    // Info
    public static void Info(string? module, string msg) => OnLog?.Invoke(LogLevel.Info, msg, module);
    public static void Info(string msg) => Info(null, msg);

    // Debug
    public static void Debug(string? module, string msg) => OnLog?.Invoke(LogLevel.Debug, msg, module);
    public static void Debug(string msg) => Debug(null, msg);

    // Trace
    public static void Trace(string? module, string msg) => OnLog?.Invoke(LogLevel.Trace, msg, module);
    public static void Trace(string msg) => Trace(null, msg);

    public static Logger CurrentLogger => LogService.Logger;
}
