using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Logging;
using PCL.Core.UI.Dialogs;

namespace PCL.Core.App.Configuration.Storage;

public class FileConfigStorage : ConfigStorage
{
    private const long SyncInterval = 10000;

    private readonly ConcurrentDictionary<string, Action> _pendingWrites = new();
    private readonly TimeSpan _stopTimeout;
    private readonly CancellationTokenSource _writeCts = new();
    private readonly Channel<byte> _writeSignal;
    private readonly Task _writerTask;
    private long _lastSyncTick;
    private int _saveFailureNotified;
    private int _stopped;

    public FileConfigStorage(IKeyValueFileProvider file)
        : this(file, TimeSpan.FromSeconds(5))
    {
    }

    internal FileConfigStorage(IKeyValueFileProvider file, TimeSpan stopTimeout)
    {
        File = file;
        _stopTimeout = stopTimeout;
        _writeSignal = Channel.CreateBounded<byte>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });
        _writerTask = Task.Run(_WriteLoopAsync);
    }

    public IKeyValueFileProvider File { get; }

    private async Task _WriteLoopAsync()
    {
        var cancelToken = _writeCts.Token;
        try
        {
            while (true)
            {
                await _writeSignal.Reader.ReadAsync(cancelToken).ConfigureAwait(false);
                var elapsed = Environment.TickCount64 - _lastSyncTick;
                if (elapsed < SyncInterval)
                    await Task
                        .Delay(
                            TimeSpan.FromMilliseconds(SyncInterval - elapsed),
                            cancelToken)
                        .ConfigureAwait(false);
                if (cancelToken.IsCancellationRequested) break;

                _Flush();
                _lastSyncTick = Environment.TickCount64;
                if (!_pendingWrites.IsEmpty) _writeSignal.Writer.TryWrite(0);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _Flush();
        }
    }

    private void _Flush()
    {
        var actions = new List<Action>();
        foreach (var key in _pendingWrites.Keys)
            if (_pendingWrites.TryRemove(key, out var action))
                actions.Add(action);
        if (actions.Count == 0) return;

        foreach (var action in actions)
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _ReportSaveFailure(ex);
            }

        try
        {
            File.Sync();
        }
        catch (Exception ex)
        {
            _ReportSaveFailure(ex);
        }
    }

    private void _ReportSaveFailure(Exception ex)
    {
        LogWrapper.Error(ex, "Config", $"配置文件保存失败：{File.FilePath}");
        if (Interlocked.Exchange(ref _saveFailureNotified, 1) != 0) return;
        if (_writeCts.IsCancellationRequested || Lifecycle.HasShutdownStarted) return;

        try
        {
            var summary = Lang.Text("Config.Error.SaveFailed.Message", File.FilePath);
            var message = ExceptionDetails.ComposeForDialog(summary, ex);
            DialogService.Instance.ShowNotification(
                new TextDialogRequest
                {
                    Title = Lang.Text("Config.Error.SaveFailed.Title"),
                    Content = message,
                    IsWarn = true
                });
        }
        catch (Exception notifyEx)
        {
            LogWrapper.Warn(notifyEx, "Config", "显示配置保存失败提示时出错");
        }
    }

    private void _QueueWrite(string key, Action action)
    {
        _pendingWrites[key] = action;
        _writeSignal.Writer.TryWrite(0);
    }

    protected override void OnStop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;

        _writeCts.Cancel();
        _writeSignal.Writer.TryWrite(0);
        try
        {
            if (!_writerTask.Wait(_stopTimeout))
                LogWrapper.Error("Config", $"停止配置写入超时：{File.FilePath}");
            else
                _writeCts.Dispose();
        }
        catch (AggregateException ex)
        {
            LogWrapper.Error(ex.GetBaseException(), "Config", $"停止配置写入失败：{File.FilePath}");
        }
    }

    protected override bool OnAccess<TKey, TValue>(
        StorageAction action,
        ref TKey key,
        [NotNullWhen(true)] ref TValue value,
        object? argument)
    {
        if (key is not string strKey)
            throw new NotSupportedException($"Key '{key}' is not supported");
#pragma warning disable CS8762
        switch (action)
        {
            case StorageAction.Get:
                if (!File.Exists(strKey)) return false;
                try
                {
                    value = File.Get<TValue>(strKey);
                }
                catch (Exception ex) when (
                    ex is JsonException
                        or InvalidCastException
                        or FormatException
                        or OverflowException
                        or ArgumentException
                        or KeyNotFoundException
                        or InvalidDataException)
                {
                    LogWrapper.Warn(ex, "Config", $"配置项 {strKey} 读取失败（可能已损坏），重置为默认值");
                    _QueueWrite(strKey, () => File.Remove(strKey));
                    return false;
                }

                return true;
            case StorageAction.Exists:
                if (typeof(TValue) == typeof(bool))
                    Unsafe.As<TValue, bool>(ref value) = File.Exists(strKey);
                else
                    throw new InvalidOperationException(
                        $"Storage action '{StorageAction.Exists}' must have a boolean value");
                return true;
            case StorageAction.Set:
                var localValue = value;
                _QueueWrite(strKey, () => File.Set(strKey, localValue));
                return false;
            case StorageAction.Delete:
                _QueueWrite(strKey, () => File.Remove(strKey));
                return false;
            default: throw new InvalidOperationException($"Invalid storage action: {action}");
        }
#pragma warning restore CS8762
    }

    public override string ToString()
    {
        return $"{base.ToString()} ({File.FilePath})";
    }
}