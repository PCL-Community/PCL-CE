using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks.Models;

/// <summary>
/// 将一个输入→输出的委托函数包装为响应式任务。
/// 对应旧版 <c>LoaderTask&lt;InputType, OutputType&gt;</c> 的迁移目标。
/// </summary>
/// <typeparam name="TInput">输入类型</typeparam>
/// <typeparam name="TOutput">输出类型</typeparam>
public class TaskDelegate<TInput, TOutput> : ITask, ITaskCancelable, ITaskProgressive
{
    private readonly Func<TaskContext<TInput, TOutput>, Task>? _asyncDelegate;
    private readonly Action<TaskContext<TInput, TOutput>>? _syncDelegate;
    private CancellationTokenSource? _cts;
    private long _lastFinishedTime;

    /// <summary>任务标题</summary>
    public string Title { get; }

    /// <summary>输入值（由调用者设置，可通过 ShouldStart 检测变化）</summary>
    public TInput? Input { get; set; }

    /// <summary>输出值，ExecuteAsync 完成后可读取</summary>
    public TOutput? Output { get; internal set; }

    /// <summary>进度权重（用于 SequentialPipelineTask 聚合进度）</summary>
    public double ProgressWeight { get; set; } = 1.0;

    /// <summary>缓存结果的有效期（毫秒），-1 表示永不超时重新执行</summary>
    public int ReloadTimeoutMs { get; set; } = -1;

    /// <summary>
    /// <see langword="true"/> 时在管道中阻塞后续任务；<see langword="false"/> 则后续任务可同时启动。
    /// </summary>
    public bool Block { get; set; } = true;


    public event TaskStateEvent? StateChanged;
    public event TaskProgressEvent? ProgressChanged;


    /// <param name="title">任务标题</param>
    /// <param name="execute">同步执行委托。通过 <c>context.Output</c> 返回结果。</param>
    /// <exception cref="ArgumentNullException">Throws if the <paramref name="title"/> or <paramref name="execute"/> is <see langword="null"/>.</exception>
    public TaskDelegate(string title, Action<TaskContext<TInput, TOutput>> execute)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        _syncDelegate = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <param name="title">任务标题</param>
    /// <param name="execute">异步执行委托。通过 <c>context.Output</c> 返回结果。</param>
    /// <exception cref="ArgumentNullException">Throws if the <paramref name="title"/> or <paramref name="execute"/> is <see langword="null"/>.</exception>
    public TaskDelegate(string title, Func<TaskContext<TInput, TOutput>, Task> execute)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        _asyncDelegate = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        var ctx = new TaskContext<TInput, TOutput>(_cts.Token)
        {
            Input = Input,
        };

        // 桥接 TaskContext 的进度事件到 ITaskProgressive
        ctx.OnProgressChanged += p => ProgressChanged?.Invoke(p);

        _UpdateState(TaskState.Running, string.Empty);

        try
        {
            if (_asyncDelegate is not null)
            {
                await _asyncDelegate(ctx).ConfigureAwait(false);
            }
            else
            {
                await Task.Run(() => _syncDelegate!(ctx), _cts.Token).ConfigureAwait(false);
            }

            // 检查是否在 delegate 执行期间被取消（取消时 OperationCanceledException 已被 catch）
            ctx.ThrowIfCancelled();

            Output = ctx.Output;
            _lastFinishedTime = Environment.TickCount64;
            _UpdateState(TaskState.Success, string.Empty);
        }
        catch (OperationCanceledException)
        {
            _UpdateState(TaskState.Canceled, "已取消");
        }
        catch (Exception ex)
        {
            _UpdateState(TaskState.Failed, ex.Message);
            throw; // 由 TaskCenter 统一捕获
        }
    }


    private void _UpdateState(TaskState newState, string msg)
    {
        State = newState;
        _UpdateState(newState, msg);
    }

    /// <inheritdoc />
    public TaskState State { get; set; }

    /// <inheritdoc />
    public void Cancel()
    {
        _cts?.Cancel();
    }

    /// <summary>
    /// 判断是否需要重新执行。当输入未变且缓存未过期时返回 <see langword="false"/>。
    /// 由调用方（如 SequentialPipelineTask）在 ExecuteAsync 前调用。
    /// </summary>
    /// <param name="input">新的输入值</param>
    /// <param name="isForceRestart">强制重启</param>
    /// <param name="ignoreReloadTimeout">忽略缓存超时</param>
    public bool ShouldStart(TInput? input, bool isForceRestart = false, bool ignoreReloadTimeout = false)
    {
        if (isForceRestart) return true;

        // 输入变化
        if (!EqualityComparer<TInput>.Default.Equals(input, Input))
            return true;

        // 没有缓存过或没有超时限制
        if (ReloadTimeoutMs == -1 || _lastFinishedTime == 0)
            return true;

        var elapsed = Environment.TickCount64 - _lastFinishedTime;
        if (ignoreReloadTimeout || elapsed < ReloadTimeoutMs)
            return false; // 缓存有效

        return true; // 缓存过期
    }

    #region Factory

    /// <summary>
    /// 创建同步执行的任务委托。输入由调用者通过 <see cref="Input"/> 属性提供。
    /// </summary>
    public static TaskDelegate<TInput, TOutput> CreateSync(
        string title,
        Action<TaskContext<TInput, TOutput>> execute)
    {
        return new TaskDelegate<TInput, TOutput>(title, execute);
    }

    /// <summary>
    /// 创建异步执行的任务委托。输入由调用者通过 <see cref="Input"/> 属性提供。
    /// </summary>
    public static TaskDelegate<TInput, TOutput> CreateAsync(
        string title,
        Func<TaskContext<TInput, TOutput>, Task> execute)
    {
        return new TaskDelegate<TInput, TOutput>(title, execute);
    }
    #endregion
}
