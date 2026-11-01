using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PCL.Core.Logging;

/// <summary>
/// Buffered, single-file-rotating log writer.
/// Producers only build a <see cref="LogEntry" /> and push it into a bounded channel, so logging costs
/// no allocation on the calling thread beyond the message itself. A single background pump drains the
/// channel, formats whole batches and writes them to disk. The pump never faults: an I/O failure
/// rebuilds the stream and, on repeated failure, discards batches during a short cooldown instead of
/// letting the channel fill up and permanently degrade into the drop path.
/// </summary>
public sealed class Logger : IAsyncDisposable
{
    private const int MaxBatchLines = 198;
    private const int StreamBufferSize = 64 * 1024;
    private const int BatchInitialCapacity = 16 * 1024;
    private const int MaxBatchCapacity = 1024 * 1024;
    private const int NotDisposed = 0;
    private const int Disposing = 1;
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Creates the log directory, opens the first file and starts the background pump.
    /// </summary>
    /// <param name="configuration">Writer configuration; <see cref="LoggerConfiguration.MinLogLevel" /> is captured once and never re-read.</param>
    public Logger(LoggerConfiguration configuration)
    {
        Configuration = configuration;
        _minRealLevel = (int)configuration.MinLogLevel.RealLevel();
        _CreateNewFile();
        _processingTask = _ProcessLogQueueAsync();
    }

    // Data stream
    private StreamWriter? _currentStream;
    private FileStream? _currentFile;
    private readonly List<string> _files = [];
    private readonly object _stateLock = new();
    // Statis
    private long _droppedCount;
    public long DroppedLogCount => Interlocked.Read(ref _droppedCount);
    // Processor
    private readonly Task _processingTask;
    private readonly int _minRealLevel;
    private long _writeBlockedUntilTicks;
    private int _disposeState;
    private readonly TaskCompletionSource _disposeDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Channel<LogEntry> _logChannel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(2048)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
        // Otherwise TryWrite could run the whole write path inline on the calling thread.
        AllowSynchronousContinuations = false
    });

    /// <summary>
    /// Snapshot of the log files created by this instance that still exist on disk, oldest first. A new
    /// array is returned on every access, so the result is safe to enumerate while the pump rotates and
    /// deletes files.
    /// </summary>
    public IReadOnlyList<string> CurrentLogFiles
    {
        get
        {
            lock (_stateLock) return _files.ToArray();
        }
    }

    /// <summary>The configuration this instance was created with.</summary>
    public LoggerConfiguration Configuration { get; }

    /// <summary>
    /// Reports whether entries of the given level reach the file. Filtering applies to file output only
    /// and never suppresses the <see cref="ActionLevel" /> side effects (hints, message boxes, fatal exits).
    /// The level is normalized through <see cref="LogLevelExtensions.RealLevel" /> because enum values
    /// carry an <see cref="ActionLevel" /> offset that changes with the build configuration.
    /// </summary>
    public bool IsEnabled(LogLevel level) => (int)level.RealLevel() >= _minRealLevel;

    /// <summary>
    /// Opens the next log file, registers it in <see cref="CurrentLogFiles" />, closes the previous
    /// stream and trims the retention window. Rotation happens once per <see cref="LoggerConfiguration.MaxFileSize" />,
    /// so closing the old handle synchronously here is cheaper than racing a fire-and-forget task.
    /// </summary>
    private void _CreateNewFile()
    {
        lock (_stateLock)
        {
            var now = DateTime.Now;
            var nameFormat = (Configuration.FileNameFormat
                              ?? $"Launch-{now.ToString("yyyy-M-d", CultureInfo.InvariantCulture)}-{{0}}") + ".log";
            var fileName = nameFormat.Replace("{0}", now.ToString("HHmmssfff", CultureInfo.InvariantCulture));
            var filePath = Path.Combine(Configuration.StoreFolder, fileName);

            var lastStream = _currentStream;
            var lastFile = _currentFile;
            _currentStream = null;
            _currentFile = null;

            Directory.CreateDirectory(Configuration.StoreFolder);
            var file = _OpenNewFile(filePath);
            _currentFile = file;
            _currentStream = new StreamWriter(file, new UTF8Encoding(false), StreamBufferSize);

            _files.Add(file.Name);

            // Close the old handles before retention runs, otherwise the files stay locked.
            try
            {
                lastStream?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }

            try
            {
                lastFile?.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }
        }

        if (!Configuration.AutoDeleteOldFile) return;

        _RunRetention();

        // Retention may have deleted files this instance created; keep the public list truthful so
        // callers never receive a path that no longer exists.
        lock (_stateLock) _files.RemoveAll(static path => !File.Exists(path));
    }

    /// <summary>
    /// Opens the file without ever overwriting an existing one. The default name has millisecond
    /// resolution, so a very small <see cref="LoggerConfiguration.MaxFileSize" /> can produce two
    /// rotations inside the same millisecond; <see cref="FileMode.Create" /> would silently truncate the
    /// earlier file in that case, so a numeric suffix is appended instead.
    /// </summary>
    private static FileStream _OpenNewFile(string path)
    {
        var extension = Path.GetExtension(path);
        var stem = path[..^extension.Length];
        for (var suffix = 0; suffix < 100; suffix++)
        {
            var candidate = suffix == 0 ? path : $"{stem}-{suffix}{extension}";
            try
            {
                return new FileStream(candidate, new FileStreamOptions
                {
                    Mode = FileMode.CreateNew,
                    Access = FileAccess.Write,
                    Share = FileShare.Read,
                    BufferSize = StreamBufferSize,
                    // Without Asynchronous every WriteAsync would block a thread-pool thread.
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                });
            }
            catch (IOException) when (File.Exists(candidate))
            {
                // Name taken by an existing file; try the next suffix.
            }
        }

        throw new IOException($"Unable to create a unique log file name for '{path}'.");
    }

    /// <summary>
    /// Deletes the oldest log files beyond <see cref="LoggerConfiguration.MaxKeepOldFile" />.
    /// Ordering is newest first, so the file currently being written is never a candidate.
    /// </summary>
    private void _RunRetention()
    {
        try
        {
            var files = Directory.GetFiles(
                    Configuration.StoreFolder,
                    "*.log",
                    SearchOption.TopDirectoryOnly)
                .Select(static x => new FileInfo(x))
                .OrderByDescending(static x => x.CreationTimeUtc)
                .Skip(Math.Max(1, Configuration.MaxKeepOldFile))
                .ToArray();

            foreach (var file in files)
            {
                try
                {
                    file.Delete();
                }
                catch (IOException)
                {
                    // ignored
                }
                catch (UnauthorizedAccessException)
                {
                    // ignored
                }
            }
        }
        catch (Exception e)
        {
            _WriteErrorLog($"Failed to clean up old log files: {e}");
        }
    }

    /// <summary>Writes a trace-level message. Prefer the lifecycle context or <c>LogWrapper</c> so the source is recorded.</summary>
    public void Trace(string message) => _Write(LogLevel.Trace, message);

    /// <summary>Writes a debug-level message. Prefer the lifecycle context or <c>LogWrapper</c> so the source is recorded.</summary>
    public void Debug(string message) => _Write(LogLevel.Debug, message);

    /// <summary>Writes an info-level message. Prefer the lifecycle context or <c>LogWrapper</c> so the source is recorded.</summary>
    public void Info(string message) => _Write(LogLevel.Info, message);

    /// <summary>Writes a warning-level message. Prefer the lifecycle context or <c>LogWrapper</c> so the source is recorded.</summary>
    public void Warn(string message) => _Write(LogLevel.Warning, message);

    /// <summary>Writes an error-level message. Prefer the lifecycle context or <c>LogWrapper</c> so the source is recorded.</summary>
    public void Error(string message) => _Write(LogLevel.Error, message);

    /// <summary>Writes a fatal-level message. Prefer the lifecycle context or <c>LogWrapper</c> so the source is recorded.</summary>
    public void Fatal(string message) => _Write(LogLevel.Fatal, message);

    /// <summary>
    /// Captures the current thread and task identity and enqueues the entry.
    /// </summary>
    private void _Write(LogLevel level, string message) => Enqueue(new LogEntry
    {
        Level = level,
        Message = message,
        Time = DateTime.Now,
        ThreadName = Thread.CurrentThread.Name,
        ThreadId = Environment.CurrentManagedThreadId,
        TaskId = Task.CurrentId ?? 0
    });

    /// <summary>
    /// Enqueues an entry with an explicit source and exception, used by the
    /// <c>Microsoft.Extensions.Logging</c> adapter.
    /// </summary>
    internal void Log(LogLevel level, string message, string? sourceName = null, Exception? exception = null)
        => Enqueue(new LogEntry
        {
            Level = level,
            Message = message,
            Time = DateTime.Now,
            SourceName = sourceName,
            ThreadName = Thread.CurrentThread.Name,
            ThreadId = Environment.CurrentManagedThreadId,
            TaskId = Task.CurrentId ?? 0,
            Exception = exception
        });

    /// <summary>
    /// Pushes the entry into the bounded channel. When the channel is full the entry is dropped and
    /// counted; the drop is reported with an exponentially decreasing rate so a log storm cannot turn
    /// into a feedback loop of console writes.
    /// </summary>
    internal void Enqueue(in LogEntry entry)
    {
        if (Volatile.Read(ref _disposeState) != NotDisposed) return;
        if (!IsEnabled(entry.Level)) return;
        if (_logChannel.Writer.TryWrite(entry)) return;

        _ReportDrop(Interlocked.Increment(ref _droppedCount));
    }

    /// <summary>
    /// Reports a drop only for counts that are powers of two (1, 2, 4, 8, ...), bounding both the number
    /// of console writes and their cost while the consumer is behind.
    /// </summary>
    private static void _ReportDrop(long count)
    {
        if ((count & (count - 1)) != 0) return;
        try
        {
            Console.Error.WriteLine($"[Logger] Log channel is full, {count} entries dropped.");
        }
        catch (Exception)
        {
            // ignored
        }
    }

    /// <summary>
    /// The consumer loop. Drains everything currently available, appending formatted lines into a reused
    /// builder, and flushes when the batch is full or the channel runs empty. When the channel is empty
    /// it blocks on <see cref="ChannelReader{T}.WaitToReadAsync" />, so an idle launcher performs zero
    /// wake-ups, and it exits only once the writer is completed and fully drained.
    /// </summary>
    private async Task _ProcessLogQueueAsync()
    {
        var reader = _logChannel.Reader;
        var batch = new StringBuilder(BatchInitialCapacity);
        var lineCount = 0;

        try
        {
            while (true)
            {
                while (reader.TryRead(out var entry))
                {
                    LogEntryFormatter.Append(batch, in entry);
                    batch.AppendLine();
#if DEBUG
                    if (Debugger.IsAttached) Console.WriteLine(LogEntryFormatter.Format(in entry));
#endif

                    if (++lineCount < MaxBatchLines) continue;

                    await _FlushAsync(batch).ConfigureAwait(false);
                    lineCount = 0;
                }

                if (lineCount > 0)
                {
                    await _FlushAsync(batch).ConfigureAwait(false);
                    lineCount = 0;
                }

                if (!await reader.WaitToReadAsync().ConfigureAwait(false)) break;
            }
        }
        catch (Exception e)
        {
            // The pump must never fault: a dead pump fills the channel and turns every later log into a drop.
            _WriteErrorLog($"Log pump terminated unexpectedly: {e}");
        }
        finally
        {
            if (batch.Length > 0)
                try
                {
                    await _FlushAsync(batch).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
        }
    }

    /// <summary>
    /// Writes and clears the batch, shrinking an oversized buffer back to the initial capacity so a
    /// single large burst does not keep a megabyte-sized builder alive for the rest of the session.
    /// </summary>
    private async Task _FlushAsync(StringBuilder batch)
    {
        if (batch.Length == 0) return;

        if (!await _WriteBatchAsync(batch).ConfigureAwait(false))
            _WriteErrorLog($"LogCycle: {batch.Length} chars discarded while the log file is unavailable.");

        batch.Clear();
        if (batch.Capacity > MaxBatchCapacity) batch.Capacity = BatchInitialCapacity;
    }

    /// <summary>
    /// Attempts the batch at most twice. A failed stream cannot be retried in place because its internal
    /// state is already broken, so the first failure disposes and reopens it. Two consecutive failures
    /// start a one second cooldown during which batches are discarded without touching the disk.
    /// </summary>
    private async Task<bool> _WriteBatchAsync(StringBuilder batch)
    {
        if (Stopwatch.GetTimestamp() < Volatile.Read(ref _writeBlockedUntilTicks)) return false;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await _WriteOnceAsync(batch).ConfigureAwait(false);
                return true;
            }
            catch (Exception e)
            {
                _WriteErrorLog($"LogCycle Error (attempt {attempt + 1}): {e}");
                if (attempt == 0) _ResetStream();
            }
        }

        Volatile.Write(ref _writeBlockedUntilTicks, Stopwatch.GetTimestamp() + Stopwatch.Frequency);
        return false;
    }

    /// <summary>
    /// Rotates the file when the current one reached the size limit and writes the batch.
    /// Rotation is checked against the on-disk length before writing, so a file may exceed the limit by
    /// at most one batch.
    /// </summary>
    private async Task _WriteOnceAsync(StringBuilder batch)
    {
        var stream = _currentStream ?? throw new IOException("Log stream is not available.");

        if (_currentFile is { } file && file.Length >= Configuration.MaxFileSize)
        {
            _CreateNewFile();
            stream = _currentStream ?? throw new IOException("Log stream is not available.");
        }

        await stream.WriteAsync(batch, CancellationToken.None).ConfigureAwait(false);
        await stream.FlushAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Drops the current stream and opens a fresh file. Does nothing once disposal started, so a failing
    /// pump cannot resurrect a log file after <see cref="DisposeAsync" /> closed it.
    /// </summary>
    private void _ResetStream()
    {
        if (Volatile.Read(ref _disposeState) != NotDisposed) return;

        StreamWriter? stream;
        FileStream? file;
        lock (_stateLock)
        {
            stream = _currentStream;
            file = _currentFile;
            _currentStream = null;
            _currentFile = null;
        }

        try
        {
            stream?.Dispose();
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            file?.Dispose();
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            _CreateNewFile();
        }
        catch (Exception e)
        {
            _WriteErrorLog($"Failed to reopen the log file: {e}");
        }
    }

    /// <summary>
    /// Appends a diagnostic line to <c>Error.log</c> next to the log files. Used by the pump itself, so it
    /// must never throw.
    /// </summary>
    private void _WriteErrorLog(string content)
    {
        try
        {
            Directory.CreateDirectory(Configuration.StoreFolder);
            File.AppendAllText(
                Path.Combine(Configuration.StoreFolder, "Error.log"),
                $"[{DateTime.Now:HH:mm:ss.fff}] {content}{Environment.NewLine}",
                Encoding.UTF8);
        }
        catch (Exception)
        {
            // Nothing left to fall back to.
        }
    }

    /// <summary>
    /// Completes the channel, waits up to three seconds for the pump to drain, then closes the stream.
    /// The method is idempotent and never throws: it is reached from the synchronous shutdown path, where
    /// an escaping exception or an unbounded wait would prevent the process from exiting.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, Disposing) != NotDisposed)
        {
            await _disposeDone.Task.ConfigureAwait(false);
            return;
        }

        try
        {
            // Close the write gate; the pump exits on its own once the queue is drained.
            _logChannel.Writer.TryComplete();

            try
            {
                await _processingTask.WaitAsync(DisposeTimeout).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _WriteErrorLog($"Log pump did not finish within {DisposeTimeout.TotalSeconds:0}s: {e}");
            }

            StreamWriter? stream;
            FileStream? file;
            lock (_stateLock)
            {
                stream = _currentStream;
                file = _currentFile;
                _currentStream = null;
                _currentFile = null;
            }

            if (stream is not null)
                try
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }

            if (file is not null)
                try
                {
                    await file.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // ignored
                }
        }
        finally
        {
            _disposeDone.TrySetResult();
        }
    }
}
