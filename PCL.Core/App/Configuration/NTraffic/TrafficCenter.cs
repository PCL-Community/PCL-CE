using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using PCL.Core.Logging;
using PCL.Core.Utils.Diagnostics;

namespace PCL.Core.App.Configuration.NTraffic;

/// <summary>
/// 物流中心上层实现。
/// </summary>
public abstract class TrafficCenter : ITrafficCenter, IConfigProvider
{
    private const string LogModule = "Traffic";

    public event TrafficEventHandler? Traffic;

    public event PreviewTrafficEventHandler? PreviewTraffic;

#if DEBUG
    private static readonly bool _EnableTrace = Basics.CommandLineArguments.Contains("--trace-traffic");
#endif

    /// <summary>
    /// 物流操作实现。
    /// </summary>
    protected abstract void OnTraffic<TInput, TOutput>(
        PreviewTrafficEventArgs<TInput, TOutput> e,
        Action<PreviewTrafficEventArgs<TInput, TOutput>> onInvokeEvent
    );

    /// <summary>
    /// 停止操作实现。
    /// </summary>
    protected abstract void OnStop();

    /// <summary>
    /// 停止该物流中心运行，保存状态并释放资源。
    /// </summary>
    public void Stop() => OnStop();

    /// <summary>
    /// 以事件参数请求标准物流操作。
    /// </summary>
    public void Request<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e)
    {
        try
        {
            OnTraffic(e, ev =>
            {
                PreviewTraffic?.Invoke(ev);
                Traffic?.Invoke(ev);
            });
        }
        catch (Exception ex)
        {
            var msg = $"NTraffic Error Report\n" +
                $"A exception was thrown while processing a request.\n\n" +
                $"[Diagnostics Info]\n{_GenerateDiagnosticsInfo(e, true)}\n\n" +
                $"[Exception Details]\n{ex}";
            LogWrapper.Fatal(msg);
            Lifecycle.ForceShutdown(-2);
        }
#if DEBUG
        if (_EnableTrace)
        {
            LogWrapper.Trace(LogModule, _GenerateDiagnosticsInfo(e));
        }
#endif
    }

    private static readonly JsonSerializerOptions _SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private string _GenerateDiagnosticsInfo<TInput, TOutput>(PreviewTrafficEventArgs<TInput, TOutput> e, bool appendCallStack = false)
    {
#if TRACE
        const bool needFileInfo = true;
#else
        const bool needFileInfo = false;
#endif
        var eventArgsName = e.ToString()?.Replace("PCL.Core.App.Configuration.NTraffic.", "");
        var context = JsonSerializer.Serialize(e.Context, _SerializerOptions);
        var input = JsonSerializer.Serialize(e.Input, _SerializerOptions);
        var output = JsonSerializer.Serialize(e.Output, _SerializerOptions);
        var caller = appendCallStack
            ? "Stack:\n|=> " + string.Join("\n|=> ", StackHelper.GetStack(includeParameters: true, needFileInfo: needFileInfo).Skip(1))
            : "Caller: " + StackHelper.GetDirectCallerName(includeParameters: true, skipAppFrames: 1);
        var msg = $"Traffic request: {e.Access} {GetType().Name}@{GetHashCode()}\n" +
            $"|- {eventArgsName}\n" +
            $"|- Context: {context}\n" +
            $"|- Input: {input} (HasInput: {e.HasInput})\n" +
            $"|- Output: {output} (HasOutput: {e.HasOutput}, IsInitialOutput: {e.IsInitialOutput})\n" +
            $"|- {caller}";
        return msg;
    }

    /// <summary>
    /// 根据已知信息创建事件参数实例。
    /// </summary>
    public static PreviewTrafficEventArgs<TInput, TOutput> CreateEventArgs<TInput, TOutput>(
        object? context, TrafficAccess access, bool hasInput, TInput? input)
    {
        var e = hasInput
            ? new PreviewTrafficEventArgs<TInput, TOutput>(input!) { Context = context, Access = access }
            : new PreviewTrafficEventArgs<TInput, TOutput> { Context = context, Access = access };
        return e;
    }

    /// <summary>
    /// 请求标准物流操作。
    /// </summary>
    public bool Request<TInput, TOutput>(object? context, TrafficAccess access,
        bool hasInput, TInput? input, bool hasInitialOutput, ref TOutput? output)
    {
        // 初始化事件参数
        var e = CreateEventArgs<TInput, TOutput>(context, access, hasInput, input);
        if (hasInitialOutput) e.SetOutput(output, true);
        Request(e);
        if (e.HasOutput) output = e.Output;
        return e.HasOutput;
    }

    /// <summary>
    /// 请求无输出值的物流操作。
    /// </summary>
    public void Request<TInput, TOutput>(object? context, TrafficAccess access, bool hasInput, TInput? input)
    {
        // 初始化事件参数
        var e = CreateEventArgs<TInput, TOutput>(context, access, hasInput, input);
        Request(e);
    }

    /// <summary>
    /// 请求有初始输出值且不接收返回值的物流操作。
    /// </summary>
    public void Request<TInput, TOutput>(object? context, TrafficAccess access, bool hasInput, TInput? input, TOutput? initialOutput)
    {
        // 初始化事件参数
        var e = CreateEventArgs<TInput, TOutput>(context, access, hasInput, input);
        e.SetOutput(initialOutput, true);
        Request(e);
    }

    #region IConfigProvider Implementation

    public bool GetValue<T>(string key, [NotNullWhen(true)] out T? value, object? argument)
    {
        T? result = default;
        var hasOutput = Request(argument, TrafficAccess.Read, true, key, false, ref result);
        value = hasOutput ? result : default;
        return hasOutput;
    }

    public void SetValue<T>(string key, T value, object? argument)
    {
        Request(argument, TrafficAccess.Write, true, key, value);
    }

    public void Delete(string key, object? argument)
    {
        Request<string, bool>(argument, TrafficAccess.Write, true, key);
    }

    public bool Exists(string key, object? argument)
    {
        var result = false;
        return Request(argument, TrafficAccess.Read, true, key, true, ref result) && result;
    }

    #endregion
}
