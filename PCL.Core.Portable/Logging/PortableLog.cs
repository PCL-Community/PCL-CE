// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

namespace PCL.Core.Logging;

public enum PortableLogLevel
{
    Trace,
    Debug,
    Info,
    Warn,
    Error
}

public readonly record struct PortableLogEntry(
    PortableLogLevel Level,
    string Module,
    string Message,
    Exception? Exception = null,
    DateTimeOffset Timestamp = default);

/// <summary>
/// Small logging bridge for portable code that must not depend on the WPF launcher lifecycle.
/// </summary>
public static class PortableLog
{
    public static event Action<PortableLogEntry>? Written;

    public static void Trace(string module, string message)
    {
        Write(new PortableLogEntry(PortableLogLevel.Trace, module, message));
    }

    public static void Debug(Exception exception, string module, string message)
    {
        Write(new PortableLogEntry(PortableLogLevel.Debug, module, message, exception));
    }

    public static void Debug(string module, string message)
    {
        Write(new PortableLogEntry(PortableLogLevel.Debug, module, message));
    }

    public static void Info(string module, string message)
    {
        Write(new PortableLogEntry(PortableLogLevel.Info, module, message));
    }

    public static void Warn(string module, string message)
    {
        Write(new PortableLogEntry(PortableLogLevel.Warn, module, message));
    }

    public static void Warn(Exception exception, string module, string message)
    {
        Write(new PortableLogEntry(PortableLogLevel.Warn, module, message, exception));
    }

    public static void Error(Exception exception, string module, string message)
    {
        Write(new PortableLogEntry(PortableLogLevel.Error, module, message, exception));
    }

    public static void Write(PortableLogEntry entry)
    {
        if (entry.Timestamp == default)
            entry = entry with { Timestamp = DateTimeOffset.UtcNow };

        Written?.Invoke(entry);
    }
}
