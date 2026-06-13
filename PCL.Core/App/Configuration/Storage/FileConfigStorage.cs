using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using PCL.Core.Logging;
using PCL.Core.UI;

namespace PCL.Core.App.Configuration.Storage;

/// <summary>
/// 文件存取仓库。
/// </summary>
public class FileConfigStorage : ConfigStorage
{
    /// <summary>
    /// 键值文件实例。
    /// </summary>
    public IKeyValueFileProvider File { get; }

    private readonly Channel<(string, Action)> _writeActionChannel;
    private readonly CancellationTokenSource _writeActionCts;
    private readonly ManualResetEventSlim _writeStopEvent = new(true);
    private readonly object _writeSyncLock = new();
    private readonly Dictionary<string, Action> _pendingWriteActions = [];
    private long _lastSyncTick;

    public FileConfigStorage(IKeyValueFileProvider file)
    {
        File = file;
        _writeActionChannel = Channel.CreateUnbounded<(string, Action)>();
        _writeActionCts = new CancellationTokenSource();
        Task.Run(async () =>
        {
            _writeStopEvent.Reset();
            const long syncInterval = 10000; // ms
            var cancelToken = _writeActionCts.Token;
            var reader = _writeActionChannel.Reader;
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    // 读入并合并暂存操作
                    var (key, action) = await reader.ReadAsync(cancelToken);
                    lock (_writeSyncLock)
                    {
                        _pendingWriteActions[key] = action;
                    }
                    if (Environment.TickCount64 - _lastSyncTick < syncInterval || cancelToken.IsCancellationRequested) continue;
                    // 同步文件
                    SyncPendingWrites();
                }
            }
            catch (OperationCanceledException) { /* ignoring*/ }
            finally
            {
                // 结束时执行一次同步
                SyncPendingWrites();
            }
            _writeStopEvent.Set();
        });
    }

    protected override void OnStop()
    {
        _writeActionCts.Cancel();
        _writeStopEvent.Wait();
        _writeStopEvent.Dispose();
    }

    protected override void OnFlush()
    {
        SyncPendingWrites();
    }

    protected override bool OnAccess<TKey, TValue>(
        StorageAction action,
        ref TKey key,
        [NotNullWhen(true)] ref TValue value,
        object? argument)
    {
        if (key is not string strKey) throw new NotSupportedException($"Key '{key}' is not supported");
#pragma warning disable CS8762 // Parameter must have a non-null value when exiting in some condition.
        switch (action)
        {
            case StorageAction.Get:
                if (!File.Exists(strKey)) return false;
                value = File.Get<TValue>(strKey);
                return true;
            case StorageAction.Exists:
                // 由于 Exists 的 value 类型一定是 bool，此处可 unsafe 直接赋值
                if (typeof(TValue) == typeof(bool)) Unsafe.As<TValue, bool>(ref value) = File.Exists(strKey);
                else throw new InvalidOperationException($"Storage action '{StorageAction.Exists}' must have a boolean value");
                return true;
            case StorageAction.Set:
                var localValue = value;
                _writeActionChannel.Writer.TryWrite((strKey, () => File.Set(strKey, localValue)));
                return false;
            case StorageAction.Delete:
                _writeActionChannel.Writer.TryWrite((strKey, () => File.Remove(strKey)));
                return false;
            default: throw new InvalidOperationException($"Invalid storage action: {action}");
        }
#pragma warning restore CS8762 // Parameter must have a non-null value when exiting in some condition.
    }

    private void SyncPendingWrites()
    {
        Dictionary<string, Action> actions;
        lock (_writeSyncLock)
        {
            if (_pendingWriteActions.Count == 0) return;
            actions = new Dictionary<string, Action>(_pendingWriteActions);
            _pendingWriteActions.Clear();
            _lastSyncTick = Environment.TickCount64;
        }

        try
        {
            LogWrapper.Trace("Config", $"正在保存 {File.FilePath}");
            foreach (var action in actions.Values) action();
            File.Sync();
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "Config", "配置文件保存失败");
            var hint = $"保存配置文件时出现问题，若该问题能够稳定复现，请尽快提交反馈。" +
                       $"\n\n错误信息:\n{ex.GetType().FullName}: {ex.Message}";
            MsgBoxWrapper.Show(hint, "配置文件保存失败", MsgBoxTheme.Error);
        }
    }

    public override string ToString() => $"{base.ToString()} ({File.FilePath})";
}
