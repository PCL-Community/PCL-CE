using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using PCL.Core.Logging;
using PCL.Core.Utils.Diagnostics;

namespace PCL.Core.App.Configuration.Storage;

public enum StorageAction
{
    Get,
    Exists,
    Set,
    Delete
}

public abstract class ConfigStorage : IConfigProvider
{
    protected abstract bool OnAccess<TInput, TOutput>(
        StorageAction action,
        ref TInput? input,
        [NotNullWhen(true)] ref TOutput? output,
        object? argument);

#if DEBUG
    private static readonly bool _EnableTrace = Basics.CommandLineArguments.Contains("--trace-traffic");
#endif

    /// <summary>
    /// 执行存取操作。
    /// </summary>
    /// <param name="action">操作类型</param>
    /// <param name="input">输入值</param>
    /// <param name="output">输出值，若无输出值则为该类型默认值</param>
    /// <param name="argument">上下文参数</param>
    /// <typeparam name="TInput">输入值类型</typeparam>
    /// <typeparam name="TOutput">输出值类型</typeparam>
    /// <returns>是否有输出值</returns>
    public bool Access<TInput, TOutput>(
        StorageAction action,
        ref TInput? input,
        [NotNullWhen(true)] ref TOutput? output,
        object? argument)
    {
        const string logModule = "Config";
        var hasOutput = false;
        try
        {
            hasOutput = OnAccess(action, ref input, ref output, argument);
        }
        catch (Exception ex)
        {
            var msg = $"Config Storage Error Report\n" +
                $"A exception was thrown while processing an access.\n\n" +
                $"[Diagnostics Info]\n{_GenerateDiagnosticsInfo(action, input, output, hasOutput, argument, true)}\n\n" +
                $"[Exception Details]\n{ex}";
            LogWrapper.Fatal(logModule, msg);
            Lifecycle.ForceShutdown(-2);
        }
#if DEBUG
        if (_EnableTrace)
        {
            LogWrapper.Trace(logModule, _GenerateDiagnosticsInfo(action, input, output, hasOutput, argument));
        }
#endif
        return hasOutput;
    }

    private static readonly JsonSerializerOptions _SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private string _GenerateDiagnosticsInfo<TInput, TOutput>(
        StorageAction accessAction,
        TInput? accessInput,
        TOutput? accessOutput,
        bool accessHasOutput,
        object? accessContext,
        bool appendCallStack = false)
    {
#if TRACE
        const bool needFileInfo = true;
#else
        const bool needFileInfo = false;
#endif
        var context = JsonSerializer.Serialize(accessContext, _SerializerOptions);
        var input = JsonSerializer.Serialize(accessInput, _SerializerOptions);
        var output = JsonSerializer.Serialize(accessOutput, _SerializerOptions);
        var caller = appendCallStack
            ? "Stack:\n|=> " + string.Join("\n|=> ", StackHelper.GetStack(includeParameters: true, needFileInfo: needFileInfo).Skip(1))
            : "Caller: " + StackHelper.GetDirectCallerName(includeParameters: true, skipAppFrames: 1);
        var msg = $"Access: {accessAction} {GetType().Name}@{GetHashCode()}\n" +
            $"|- Context: {context}\n" +
            $"|- Input: {input}\n" +
            $"|- Output: {output} (HasOutput: {accessHasOutput})\n" +
            $"|- {caller}";
        return msg;
    }

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
}
