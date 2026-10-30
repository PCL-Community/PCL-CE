using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.App.IoC;

partial class Lifecycle
{
    private const int MaxPendingLogs = 4096;
    private static ILifecycleLogService? _logService;
    private static readonly List<LifecycleLogItem> _PendingLogs = [];
    private static int _droppedPendingLogs;

    /// <summary>
    /// Forwards one item to the log service. Deliberately called outside the pending-list lock because a
    /// log item may open a modal window and start a nested message loop.
    /// </summary>
    private static void _PushLog(LifecycleLogItem item, ILifecycleLogService service)
    {
        service.OnLog(item);
    }

    /// <summary>
    /// Buffers an item produced before the log service started, up to <see cref="MaxPendingLogs" />.
    /// Beyond that the item is dropped and counted, so a failure to start the log service cannot grow the
    /// buffer without bound. The caller must hold the pending-list lock.
    /// </summary>
    private static void _AddPendingLog(LifecycleLogItem item)
    {
        if (_PendingLogs.Count < MaxPendingLogs)
        {
            _PendingLogs.Add(item);
            return;
        }
        _droppedPendingLogs++;
    }

    public static string PendingLogDirectory { get; set; } = @"PCL\Log";
    public static string PendingLogFileName { get; set; } = "LastPending.log";

    /// <summary>
    /// Whether the log service has been started and buffered items are being forwarded.
    /// </summary>
    public static bool IsLogServiceStarted => _logService is not null;

    /// <summary>
    /// Dumps the still-buffered items to <see cref="PendingLogFileName" />. Used when the process exits
    /// before the log service started, typically after an early crash, so the buffered items are the only
    /// diagnostic left. Falls back to stdout when the file cannot be written.
    /// </summary>
    private static void _SavePendingLogs()
    {
        LifecycleLogItem[] pending;
        int dropped;
        lock (_PendingLogs)
        {
            pending = _PendingLogs.ToArray();
            dropped = _droppedPendingLogs;
        }

        if (pending.Length == 0)
        {
            Console.WriteLine("[Lifecycle] No pending logs");
            return;
        }
        try
        {
            // Write the remaining output straight into the program directory.
            var path = Path.Combine(PendingLogDirectory, PendingLogFileName);
            if (!Path.IsPathRooted(path)) path = Path.Combine(Basics.ExecutableDirectory, path);
            Directory.CreateDirectory(Basics.GetParentPathOrDefault(path));
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            foreach (var item in pending) writer.WriteLine(item.ComposeMessage());
            if (dropped > 0)
                writer.WriteLine($"[-] [WARN] [{dropped} pending log entries were dropped]");
            Console.WriteLine($"[Lifecycle] Pending logs saved to {path}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            Console.WriteLine("[Lifecycle] Error saving pending logs, writing to stdout...");
            foreach (var item in pending) Console.WriteLine(item.ComposeMessage());
        }
    }
}

/// <summary>
/// A log item raised through a <see cref="LifecycleContext" />. The item captures the time, thread and
/// task identity at construction using plain field reads only, so producing one allocates nothing; the
/// text is materialized later by <see cref="LogEntryFormatter" /> on the writer thread.
/// </summary>
/// <param name="Source">Service that produced the item, or <see langword="null" /> for the system context.</param>
/// <param name="Message">Raw message text.</param>
/// <param name="Exception">Related exception, if any.</param>
/// <param name="Level">Log level, possibly carrying an <see cref="ActionLevel" /> offset.</param>
/// <param name="ActionLevel">User-visible action to perform in addition to writing the file.</param>
public readonly record struct LifecycleLogItem(
    ILifecycleService? Source,
    string Message,
    Exception? Exception,
    LogLevel Level,
    ActionLevel ActionLevel)
{
    /// <summary>
    /// Local time at which the item was created. Captured eagerly so the timestamp reflects the producer,
    /// not the writer thread that later formats it.
    /// </summary>
    public DateTime Time { get; } = DateTime.Now;

    /// <summary>
    /// Managed id of the thread that created the item, captured eagerly for the same reason as
    /// <see cref="Time" />.
    /// </summary>
    public int ThreadId { get; } = Environment.CurrentManagedThreadId;

    /// <summary>
    /// Name of the thread that created the item, or <see langword="null" /> when the thread is unnamed.
    /// </summary>
    public string? ThreadName { get; } = Thread.CurrentThread.Name;

    /// <summary>
    /// Id of the <see cref="Task" /> that created the item, or 0 when it was created outside a task.
    /// </summary>
    public int TaskId { get; } = Task.CurrentId ?? 0;

    /// <summary>
    /// Converts the item into the shared <see cref="LogEntry" /> representation without building any text,
    /// so both logging paths produce identical output.
    /// </summary>
    internal LogEntry ToLogEntry() => new()
    {
        Level = Level,
        Message = Message,
        Time = Time,
        SourceName = Source?.Name,
        SourceId = Source?.Identifier,
        ThreadName = ThreadName,
        ThreadId = ThreadId,
        TaskId = TaskId,
        Exception = Exception,
    };

    /// <inheritdoc />
    public override string ToString() => LogEntryFormatter.Format(ToLogEntry());

    /// <summary>
    /// Renders the item as a complete log line using the same formatter as the file writer, so the pending
    /// dump stays readable with the regular log format.
    /// </summary>
    public string ComposeMessage() => LogEntryFormatter.Format(ToLogEntry());
}

/// <summary>
/// Interface implemented by the log service. Only one log service may exist in a lifecycle; registering a
/// second one throws during startup.
/// </summary>
public interface ILifecycleLogService : ILifecycleService
{
    /// <summary>
    /// Called for every log item once the service has started.
    /// </summary>
    public void OnLog(LifecycleLogItem item);
}
