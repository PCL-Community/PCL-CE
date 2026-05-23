using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks.Models;

/// <summary>
/// 顺序执行步骤管线的任务。
/// 对应旧版 <c>LoaderCombo&lt;A&gt;</c> / <c>LoaderCombo</c> 的迁移目标。
/// <para>
/// 每个步骤通过 <see cref="AddStep{TInput,TOutput}"/>
/// 或 <see cref="AddTask"/>（<see cref="ITask"/> 包装）添加，
/// 按添加顺序依次执行，支持加权进度汇报。
/// </para>
/// </summary>
/// <typeparam name="TContext">步骤间共享的上下文类型，必须为可实例化的引用类型</typeparam>
public class SequentialPipelineTask<TContext> : ITask, ITaskCancelable, ITaskProgressive
    where TContext : class, new()
{
    private readonly List<IPipelineStep<TContext>> _steps = [];
    private CancellationTokenSource? _cts;

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public event TaskStateEvent? StateChanged;

    /// <inheritdoc />
    public event TaskProgressEvent? ProgressChanged;

    public SequentialPipelineTask(string title)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
    }

    /// <summary>
    /// 添加一个同步执行步骤。
    /// 函数签名中的 <typeparamref name="TInput"/> 和 <typeparamref name="TOutput" />
    /// 为 lambda 提供类型安全性，返回值不会被管道消费——步骤间的数据交换应通过
    /// <typeparamref name="TContext"/> 完成。
    /// </summary>
    /// <typeparam name="TInput">本步骤的输入类型（仅用于 lambda 类型推断）</typeparam>
    /// <typeparam name="TOutput">本步骤的输出类型（仅用于 lambda 类型推断）</typeparam>
    /// <param name="name">步骤名称</param>
    /// <param name="execute">执行函数：(上下文, default(TInput)) → TOutput?（返回值被忽略）</param>
    /// <param name="weight">进度权重</param>
    public SequentialPipelineTask<TContext> AddStep<TInput, TOutput>(
        string name,
        Func<TContext, TInput?, TOutput?> execute,
        double weight = 1)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _steps.Add(new SyncStep<TInput, TOutput>(name, execute, weight));
        return this;
    }

    /// <summary>
    /// 添加一个异步执行步骤。
    /// 函数签名中的 <typeparamref name="TInput"/> 和 <typeparamref name="TOutput" />
    /// 为 lambda 提供类型安全性，返回值不会被管道消费——步骤间的数据交换应通过
    /// <typeparamref name="TContext"/> 完成。
    /// </summary>
    /// <typeparam name="TInput">本步骤的输入类型（仅用于 lambda 类型推断）</typeparam>
    /// <typeparam name="TOutput">本步骤的输出类型（仅用于 lambda 类型推断）</typeparam>
    /// <param name="name">步骤名称</param>
    /// <param name="execute">异步执行函数：(上下文, default(TInput), 取消令牌) → Task（返回值被忽略）</param>
    /// <param name="weight">进度权重</param>
    public SequentialPipelineTask<TContext> AddStep<TInput, TOutput>(
        string name,
        Func<TContext, TInput?, CancellationToken, Task<TOutput?>> execute,
        double weight = 1)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _steps.Add(new AsyncStep<TInput, TOutput>(name, execute, weight));
        return this;
    }

    /// <summary>
    /// 添加一个 <see cref="ITask"/> 作为步骤。
    /// 该任务不参与输入/输出链式传递（其输出不会被管道消费）。
    /// </summary>
    /// <param name="task">要包装的任务实例</param>
    public SequentialPipelineTask<TContext> AddTask(ITask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        _steps.Add(new TaskStep(task));
        return this;
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        var ct = _cts.Token;

        var context = new TContext();
        var totalWeight = _steps.Sum(static s => s.Weight);

        _UpdateState(TaskState.Running, string.Empty);

        try
        {
            double completedWeight = 0;

            foreach (var step in _steps)
            {
                ct.ThrowIfCancellationRequested();

                // 如果步骤支持子进度（ITaskProgressive），桥接其进度
                // 子进度在该步骤的权重区间内插值
                IDisposable? progressBridge = null;
                if (step is ITaskProgressive progressive && totalWeight > 0)
                {
                    var stepBase = completedWeight;
                    TaskProgressEvent handler = subProgress =>
                        ProgressChanged?.Invoke((stepBase + step.Weight * subProgress) / totalWeight);
                    progressive.ProgressChanged += handler;
                    progressBridge = new DisposeAction(() => progressive.ProgressChanged -= handler);
                }

                try
                {
                    await step.ExecuteAsync(context, ct).ConfigureAwait(false);
                }
                finally
                {
                    progressBridge?.Dispose();
                }

                completedWeight += step.Weight;

                // 报告当前步骤完成后的整体进度
                if (totalWeight > 0)
                    ProgressChanged?.Invoke(completedWeight / totalWeight);
            }

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
        StateChanged?.Invoke(newState, msg);
    }

    /// <inheritdoc />
    public TaskState State { get; set; }

    /// <inheritdoc />
    public void Cancel() => _cts?.Cancel();

    /// <summary>
    /// 同步步骤：将 <see cref="Func{TContext, TInput, TOutput}"/> 适配为 <see cref="IPipelineStep{TContext}"/>。
    /// </summary>
    private sealed class SyncStep<TInput, TOutput> : IPipelineStep<TContext>
    {
        private readonly string _name;
        private readonly double _weight;
        private readonly Func<TContext, TInput?, TOutput?> _execute;

        public string Name => _name;
        public bool Block => true;   // 同步步骤天然阻塞
        public double Weight => _weight;

        public SyncStep(string name, Func<TContext, TInput?, TOutput?> execute, double weight)
        {
            _name = name;
            _execute = execute;
            _weight = weight;
        }

        public Task ExecuteAsync(TContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.Run(() => _execute(context, default), ct);
        }
    }

    /// <summary>
    /// 异步步骤：将 <see cref="Func{TContext, TInput, CancellationToken, Task{TOutput}}"/> 适配为 <see cref="IPipelineStep{TContext}"/>。
    /// </summary>
    private sealed class AsyncStep<TInput, TOutput> : IPipelineStep<TContext>
    {
        private readonly string _name;
        private readonly double _weight;
        private readonly Func<TContext, TInput?, CancellationToken, Task<TOutput?>> _execute;

        public string Name => _name;
        public bool Block => true;
        public double Weight => _weight;

        public AsyncStep(string name, Func<TContext, TInput?, CancellationToken, Task<TOutput?>> execute, double weight)
        {
            _name = name;
            _execute = execute;
            _weight = weight;
        }

        public Task ExecuteAsync(TContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return _execute(context, default, ct);
        }
    }

    /// <summary>
    /// 任务步骤：将 <see cref="ITask"/> 包装为 <see cref="IPipelineStep{TContext}"/>。
    /// 如果底层任务实现了 <see cref="ITaskProgressive"/>，其子进度会被管道桥接。
    /// </summary>
    private sealed class TaskStep : IPipelineStep<TContext>, ITaskProgressive
    {
        private readonly ITask _task;

        public string Name => _task.Title;
        public bool Block => true;
        public double Weight { get; }

        public event TaskProgressEvent? ProgressChanged;

        public TaskStep(ITask task)
        {
            _task = task;

            // 尝试从 TaskDelegate 上获取自定义权重
            // 通过反射无法避免，但 .NET 运行时会缓存这一检查
            var type = task.GetType();
            var prop = type.GetProperty("ProgressWeight");
            Weight = prop?.GetValue(task) is double d ? d : 1.0;

            // 桥接子任务进度
            if (task is ITaskProgressive progressive)
            {
                progressive.ProgressChanged += p => ProgressChanged?.Invoke(p);
            }
        }

        public Task ExecuteAsync(TContext context, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return _task.ExecuteAsync(ct);
        }
    }

    /// <summary>
    /// 用于在 finally 块中解除事件绑定的辅助类型。
    /// </summary>
    private sealed class DisposeAction(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
