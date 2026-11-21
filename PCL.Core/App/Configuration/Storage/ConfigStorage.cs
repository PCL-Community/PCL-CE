using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using PCL.Core.Logging;
using PCL.Core.Utils;
using PCL.Core.Utils.Diagnostics;

namespace PCL.Core.App.Configuration.Storage;

public enum StorageAction
{
    Get,
    Exists,
    Set,
    Delete
}

/// <summary>存取仓库模型实现与底层抽象。</summary>
public abstract class ConfigStorage : IConfigProvider
{
#if DEBUG
    private static readonly bool _EnableTrace =
        Basics.CommandLineArguments.Contains("--trace-traffic");
#endif

    private static readonly JsonSerializerOptions _SerializerOptions =
        new(JsonCompat.SerializerOptions)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

    private int _failureCount;

    private long _lastFailureLogTick;

    public bool GetValue<T>(string key, [NotNullWhen(true)] out T? value, object? argument = null)
    {
        var keyRef = key;
        T? valueRef = default;
        var hasValue = Access(StorageAction.Get, ref keyRef, ref valueRef, argument);
        value = valueRef;
        return hasValue;
    }

    public void SetValue<T>(string key, T value, object? argument = null)
    {
        var keyRef = key;
        var valueRef = value;
        Access(StorageAction.Set, ref keyRef, ref valueRef, argument);
    }

    public void Delete(string key, object? argument = null)
    {
        var keyRef = key;
        object? valueRef = null;
        Access(StorageAction.Delete, ref keyRef, ref valueRef, argument);
    }

    public bool Exists(string key, object? argument = null)
    {
        var keyRef = key;
        var resultRef = false;
        return Access(StorageAction.Exists, ref keyRef, ref resultRef, argument) && resultRef;
    }

    protected abstract bool OnAccess<TKey, TValue>(
        StorageAction action,
        ref TKey key,
        [NotNullWhen(true)] ref TValue value,
        object? argument);

    protected virtual void OnStop()
    {
    }

    /// <summary>停止存取工作，保存并释放资源。</summary>
    public void Stop()
    {
        OnStop();
    }

    /// <summary>执行存取操作。</summary>
    /// <param name="action">操作类型</param>
    /// <param name="key">键</param>
    /// <param name="value">值，若无值则为该类型默认值</param>
    /// <param name="argument">上下文参数</param>
    /// <typeparam name="TKey">键的类型</typeparam>
    /// <typeparam name="TValue">值的类型</typeparam>
    /// <returns>是否有输出值</returns>
    public bool Access<TKey, TValue>(
        StorageAction action,
        ref TKey key,
        [NotNullWhen(true)] ref TValue value,
        object? argument)
    {
        const string logModule = "Config";
        var hasOutput = false;
        try
        {
            hasOutput = OnAccess(action, ref key, ref value, argument);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Interlocked.Increment(ref _failureCount);
            var now = Environment.TickCount64;
            var lastLogTick = Interlocked.Read(ref _lastFailureLogTick);
            if (lastLogTick == 0 || now - lastLogTick >= 60000)
            {
                Interlocked.Exchange(ref _lastFailureLogTick, now);
                var msg = $"Config Storage Error Report\n"
                    + $"An exception was thrown while processing an access.\n\n"
                    + $"[Failure Count] {Interlocked.CompareExchange(ref _failureCount, 0, 0)}\n\n"
                    + $"[Diagnostics Info]\n{_TryGenerateDiagnosticsInfo(action, key, value, hasOutput, argument, true)}\n\n"
                    + $"[Exception Details]\n{ex}";
                LogWrapper.Error(ex, logModule, msg);
            }
        }
#if DEBUG
        if (_EnableTrace)
            LogWrapper.Trace(
                logModule,
                _TryGenerateDiagnosticsInfo(action, key, value, hasOutput, argument));
#endif
        return hasOutput;
    }

    private string _TryGenerateDiagnosticsInfo<TKey, TValue>(
        StorageAction accessAction,
        TKey? accessKey,
        TValue? accessValue,
        bool accessHasValue,
        object? accessContext,
        bool appendCallStack = false)
    {
        try
        {
            return _GenerateDiagnosticsInfo(
                accessAction,
                accessKey,
                accessValue,
                accessHasValue,
                accessContext,
                appendCallStack);
        }
        catch (Exception ex)
        {
            return $"Storage Access: {accessAction} {ToString()}\n"
                + $"|- Key type: {typeof(TKey).Name}\n"
                + $"|- Value type: {typeof(TValue).Name}\n"
                + $"|- Diagnostics unavailable: {ex.GetType().Name}";
        }
    }

    private string _GenerateDiagnosticsInfo<TKey, TValue>(
        StorageAction accessAction,
        TKey? accessKey,
        TValue? accessValue,
        bool accessHasValue,
        object? accessContext,
        bool appendCallStack = false)
    {
#if TRACE
        const bool needFileInfo = true;
#else
        const bool needFileInfo = false;
#endif
        var context = JsonSerializer.Serialize(accessContext, _SerializerOptions);
        var key = JsonSerializer.Serialize(accessKey, _SerializerOptions);
        var caller = appendCallStack
            ? "Stack:\n|=> "
            + string.Join(
                "\n|=> ",
                StackHelper.GetStack(includeParameters: true, needFileInfo: needFileInfo).Skip(1))
            : "Caller: "
            + StackHelper.GetDirectCallerName(includeParameters: true, skipAppFrames: 2);
        var msg = $"Storage Access: {accessAction} {ToString()}\n"
            + $"|- Context: {(accessContext is null ? "" : "(" + accessContext.GetType().Name + ") ")}{context}\n"
            + $"|- Key: ({typeof(TKey).Name}) {key}\n"
            + $"|- Value: ({typeof(TValue).Name}) {(accessHasValue ? "<redacted>" : "undefined")}\n"
            + $"|- {caller}";
        return msg;
    }

    public override string ToString()
    {
        return $"{GetType().Name}@{GetHashCode()}";
    }
}