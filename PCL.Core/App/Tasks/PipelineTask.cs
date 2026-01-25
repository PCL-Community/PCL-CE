using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks;

public class PipelineReturnList : List<object?>;

/// <summary>
/// 管道任务。<br/>
/// 后一个委托的参数会传入前一个委托的返回值。<br/>
/// 若需要获取或修改任务信息，传入的委托第一个参数必须为 <see cref="TaskBase"/> 或 <see cref="TaskBase{TResult}"/>。<br/>
/// 若需向下一委托传入多个参数，请以 <see cref="PipelineReturnList"/> 对象作为返回值。
/// </summary>
/// <typeparam name="TLastResult">最终的返回类型。如果最终返回类型为 <see cref="System.Void"/></typeparam>
public class PipelineTask<TLastResult> : TaskGroup<TLastResult>
{
    public PipelineTask(string name, IList<TaskBase> taskBases, CancellationToken? cancellationToken = null, string? description = null) : base(name, taskBases, cancellationToken, description)
    {
        if (!taskBases.Last().ResultType.IsAssignableTo(typeof(TLastResult)))
            throw new InvalidCastException($"[PipelineTask - {name}] 构造失败：不匹配的返回类型。");
    }
    public PipelineTask(string name, IList<Delegate> delegates, CancellationToken? cancellationToken = null, string? description = null) : base(name, delegates, cancellationToken, description)
    {
        if (!delegates.Last().Method.ReturnType.IsAssignableTo(typeof(TLastResult)))
            throw new InvalidCastException($"[PipelineTask - {name}] 构造失败：不匹配的返回类型。");
    }

    public override TLastResult Run(params object[] objects)
    {
        if (State != TaskState.Waiting)
            throw new InvalidOperationException($"[PipelineTask - {Name}] 运行失败：任务已执行");
        State = TaskState.Running;
        try
        {
            object? lastResult = new();
            foreach (var task in Tasks)
                task.ProgressChanged += (_, o, n) =>
                    Progress += (n - o) / Tasks.Count;
            for (var i = 0; i < Tasks.Count; i++)
            {
                object[] param = [];
                if (lastResult != null)
                    param = [lastResult];
                if (lastResult?.GetType().IsAssignableTo(typeof(IList<object>)) ?? false)
                    param = ((IList<object>)lastResult).ToArray();
                if (i == 0)
                    param = objects;
                CancellationToken?.ThrowIfCancellationRequested();
                lastResult = Tasks[i].Run(param);
            }
            State = TaskState.Completed;
            if (lastResult != null)
                return Result = (TLastResult)lastResult;
            if (typeof(TLastResult) != typeof(VoidResult))
                throw new NullReferenceException($"[PipelineTask - {Name}] 最后的结果是空的。");
            return (TLastResult)(object)new VoidResult();
        }
        catch (Exception)
        {
            if (!(CancellationToken?.IsCancellationRequested ?? false))
                State = TaskState.Failed;
            throw;
        }
    }

    public override async Task<TLastResult> RunAsync(params object[] objects)
    {
        if (State != TaskState.Waiting)
            throw new InvalidOperationException($"[PipelineTask - {Name}] 运行失败：任务已执行");
        State = TaskState.Running;
        try
        {
            foreach (var task in Tasks)
                task.ProgressChanged += (_, o, n) =>
                    Progress += (n - o) / Tasks.Count;
            object? lastResult = new();
            for (var i = 0; i < Tasks.Count; i++)
            {
                object[] param = [];
                if (lastResult != null)
                    param = [lastResult];
                if (lastResult?.GetType().IsAssignableTo(typeof(IList<object>)) ?? false)
                    param = ((IList<object>)lastResult).ToArray();
                if (i == 0)
                    param = objects;
                CancellationToken?.ThrowIfCancellationRequested();
                lastResult = await Tasks[i].RunAsync(param);
            }
            State = TaskState.Completed;
            if (lastResult != null)
                return Result = (TLastResult)lastResult;
            if (typeof(TLastResult) != typeof(VoidResult))
                throw new NullReferenceException($"[PipelineTask - {Name}] 最后的结果是空的。");
            return (TLastResult)(object)new VoidResult();
        }
        catch (Exception)
        {
            if (!(CancellationToken?.IsCancellationRequested ?? false))
                State = TaskState.Failed;
            throw;
        }
    }

    public override void RunBackground(params object[] objects)
        => RunAsync(objects).Start();
}