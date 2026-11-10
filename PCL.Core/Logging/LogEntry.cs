using System;
using System.Globalization;
using System.Text;

namespace PCL.Core.Logging;

/// <summary>
/// A single log record. Producers only fill the fields and enqueue the value; every string operation
/// (timestamp formatting, level tag, source prefix and exception stack rendering) is deferred to the
/// single consumer thread. This keeps the calling thread - and the lifecycle log lock it may hold -
/// free of temporary string allocations.
/// </summary>
internal readonly struct LogEntry
{
    /// <summary>Severity of the entry. May carry an <see cref="ActionLevel" /> offset, so compare via <see cref="LogLevelExtensions.RealLevel" />.</summary>
    public required LogLevel Level { get; init; }

    /// <summary>Raw message text exactly as supplied by the caller. Never prefixed or formatted here.</summary>
    public required string Message { get; init; }

    /// <summary>Local time at which the entry was produced, used to render the timestamp on the consumer thread.</summary>
    public DateTime Time { get; init; }

    /// <summary>Human readable producer name: the <c>module</c> argument of <c>LogWrapper</c> or the <c>Name</c> of a lifecycle service.</summary>
    public string? SourceName { get; init; }

    /// <summary>Stable producer identifier, set by lifecycle services only (<c>Identifier</c>). <see langword="null" /> for the <c>LogWrapper</c> path.</summary>
    public string? SourceId { get; init; }

    /// <summary>Name of the thread that produced the entry, or <see langword="null" /> when the thread is unnamed.</summary>
    public string? ThreadName { get; init; }

    /// <summary>Managed thread id captured at production time, used as a fallback when <see cref="ThreadName" /> is empty.</summary>
    public int ThreadId { get; init; }

    /// <summary>Id of the <see cref="System.Threading.Tasks.Task" /> that produced the entry, or 0 when not running inside a task.</summary>
    public int TaskId { get; init; }

    /// <summary>Related exception. Its stack trace is rendered only when the batch is actually written to disk.</summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// The single formatting implementation for <see cref="LogEntry" />, shared by the file writer and by
/// the pending-log dump so both produce byte-identical output. Output has no trailing newline.
/// </summary>
internal static class LogEntryFormatter
{
    private const string TimeFormat = "HH:mm:ss.fff";

    /// <summary>
    /// Formats the entry into a complete line without a trailing newline. Allocates one builder sized
    /// after the message length, then one result string.
    /// </summary>
    public static string Format(in LogEntry entry)
    {
        var builder = new StringBuilder(entry.Message.Length + 64);
        Append(builder, in entry);
        return builder.ToString();
    }

    /// <summary>
    /// Appends the formatted line to <paramref name="builder" /> without a trailing newline.
    /// The timestamp is rendered into a stack buffer, the level tag is a compile-time constant, and the
    /// exception stack is materialized at most once per entry.
    /// </summary>
    public static void Append(StringBuilder builder, in LogEntry entry)
    {
        // Fixed width format so the column stays aligned for every entry.
        Span<char> timeBuffer = stackalloc char[12];
        if (entry.Time.TryFormat(timeBuffer, out var written, TimeFormat, CultureInfo.InvariantCulture))
            builder.Append('[').Append(timeBuffer[..written]).Append(']');
        else
            builder.Append("[--:--:--.---]");

        builder.Append(" [").Append(_Tag(entry.Level)).Append("] [");

        if (entry.TaskId != 0)
            builder.Append("TSK#").Append(entry.TaskId);
        else if (!string.IsNullOrEmpty(entry.ThreadName))
            builder.Append(entry.ThreadName);
        else
            builder.Append('#').Append(entry.ThreadId);

        builder.Append(']');

        if (!string.IsNullOrEmpty(entry.SourceName))
        {
            builder.Append(" [").Append(entry.SourceName);
            if (!string.IsNullOrEmpty(entry.SourceId)) builder.Append('|').Append(entry.SourceId);
            builder.Append(']');
        }
        else if (!string.IsNullOrEmpty(entry.SourceId))
        {
            builder.Append(" [").Append(entry.SourceId).Append(']');
        }

        builder.Append(' ').Append(entry.Message);

        if (entry.Exception is not null)
            builder.Append('\n').Append(entry.Exception);
    }

    /// <summary>
    /// Maps the level to its fixed three or four character tag. A switch over enum constants compiles to
    /// a jump table, which is cheaper than the dictionary lookup it replaces and never allocates.
    /// </summary>
    private static string _Tag(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRA",
        LogLevel.Debug => "DBG",
        LogLevel.Info => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERR!",
        LogLevel.Fatal => "FTL!",
        _ => "????",
    };
}
