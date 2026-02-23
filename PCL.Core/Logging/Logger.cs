using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using PCL.Core.Utils.Exts;

namespace PCL.Core.Logging;

public sealed class Logger : IDisposable
{
    public Logger(LoggerConfiguration configuration)
    {
        Configuration = configuration;
        _CreateNewFile();
        _processingTask = _ProcessLogQueueAsync(_cancelToken.Token);
    }
    // Data stream
    private StreamWriter? _currentStream;
    private FileStream? _currentFile;
    private readonly List<string> _files = [];
    // Statis
    private long _droppedCount;
    public long DroppedLogCount => Interlocked.Read(ref _droppedCount);
    // Processor
    private readonly Task _processingTask;
    private readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions()
    {
        SingleReader = true
    });
    private readonly CancellationTokenSource _cancelToken = new();

    public ReadOnlyCollection<string> CurrentLogFiles => _files.AsReadOnly();

    public LoggerConfiguration Configuration { get; }

    private void _CreateNewFile()
    {
        var nameFormat = (Configuration.FileNameFormat ?? $"Launch-{DateTime.Now:yyyy-M-d}-{{0}}") + ".log";
        var filename = nameFormat.Replace("{0}", $"{DateTime.Now:HHmmssfff}");
        var filePath = Path.Combine(Configuration.StoreFolder, filename);
        _files.Add(filePath);
        var lastWriter = _currentStream;
        var lastFile = _currentFile;
        Directory.CreateDirectory(Configuration.StoreFolder);

        _currentFile = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _currentStream = new StreamWriter(_currentFile);

        _ = Task.Run(async () =>
        {
            if (lastWriter != null)
                try
                {
                    await lastWriter.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception) { /* Don't care */ }

            if (lastFile != null)
                try
                {
                    await lastFile.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception) { /* Don't care */ }

            if (!Configuration.AutoDeleteOldFile)
                return;

            var logFiles = Directory.GetFiles(
                Configuration.StoreFolder,
                "*.log",
                SearchOption.TopDirectoryOnly);
            var needToDelete = logFiles.Select(x => new FileInfo(x))
                .OrderBy(x => x.CreationTime)
                .Take(logFiles.Length - Configuration.MaxKeepOldFile);
            foreach (var logFile in needToDelete)
                logFile.Delete();
        });
    }

    public void Trace(string message) => Log($"[{_GetTimeFormatted()}] [TRA] {message}");
    public void Debug(string message) => Log($"[{_GetTimeFormatted()}] [DBG] {message}");
    public void Info(string message) => Log($"[{_GetTimeFormatted()}] [INFO] {message}");
    public void Warn(string message) => Log($"[{_GetTimeFormatted()}] [WARN] {message}");
    public void Error(string message) => Log($"[{_GetTimeFormatted()}] [ERR!] {message}");
    public void Fatal(string message) => Log($"[{_GetTimeFormatted()}] [FTL!] {message}");
    
    private static string _GetTimeFormatted() => $"{DateTime.Now:HH:mm:ss.fff}";
    
    public void Log(string message)
    {
        if (_disposed) return;
        if (!_logChannel.Writer.TryWrite(message))
        {
            Interlocked.Increment(ref _droppedCount);
            Console.WriteLine($"Log dropped error: {message}");
        }
    }

    private async Task _ProcessLogQueueAsync(CancellationToken token)
    {
        const int maxBatchLines = 198;
        var writeTimeout = TimeSpan.FromMilliseconds(325);
        var batch = new StringBuilder(4096);
        var lineCount = 0u;
        var lastFlush = Stopwatch.GetTimestamp();

        try
        {
            while (!token.IsCancellationRequested || _logChannel.Reader.Count != 0)
            {
                if (_logChannel.Reader.TryRead(out var message))
                {
#if DEBUG
                    message = message.ReplaceLineBreak("\r\n");
                    Console.WriteLine(message);
                    System.Diagnostics.Debug.WriteLine(message);
#endif
                    batch.AppendLine(message);
                    lineCount++;

                    var elapsed = Stopwatch.GetElapsedTime(lastFlush);
                    if (lineCount >= maxBatchLines || elapsed > writeTimeout)
                    {
                        await DoRefreshAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    if (lineCount != 0)
                    {
                        await DoRefreshAsync().ConfigureAwait(false);
                    }
                    await Task.Delay(80, token).ConfigureAwait(false);
                }
            }

            async Task DoRefreshAsync()
            {
                await _DoWriteAsync(batch).ConfigureAwait(false);
                batch.Clear();
                lineCount = 0;
                lastFlush = Stopwatch.GetTimestamp();
            }
        }
        catch (OperationCanceledException)
        {
            if (lineCount > 0)
                await _DoWriteAsync(batch).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            // 出错了先干到标准输出流中吧 Orz
            Console.WriteLine($"[{_GetTimeFormatted()}] [ERROR] An error occured while processing log queue: {e.Message}");
            throw;
        }
    }

    private async Task _DoWriteAsync(StringBuilder ctx)
    {
        try
        {
            if (_currentFile?.Length >= Configuration.MaxFileSize)
            {
                _CreateNewFile();
            }
            await _currentStream!.WriteAsync(ctx).ConfigureAwait(false);
            await _currentStream.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[{_GetTimeFormatted()}] [ERROR] An error occured while writing log file: {e.Message}");
            throw;
        }
    }

    private bool _disposed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancelToken.Cancel();
        _processingTask.Forget();
        _processingTask.ContinueWith(_ =>
        {
            _logChannel.Writer.Complete();
            _currentStream?.Dispose();
            _currentFile?.Dispose();
        }).Forget();
    }
}