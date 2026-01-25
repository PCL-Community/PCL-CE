using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks;

public struct VoidResult;

/// <summary>
/// 任务原型。<br/>
/// 若需要获取或修改任务信息，传入的委托第一个参数必须为 <see cref="TaskBase"/>。
/// </summary>
public class TaskBase : IObservableTaskStateSource, IObservableProgressSource, IDisposable
{
    public TaskBase()
    {
        Name = "";
        Delegate = () => { };
    }
    protected TaskBase(string name, CancellationToken? cancellationToken = null, string? description = null)
    {
        Name = name;
        Description = description;
        CancellationToken = cancellationToken;
        Delegate = () => { };
    }
    public TaskBase(string name, Delegate loadDelegate, CancellationToken? cancellationToken = null, string? description = null)
    {
        Name = name;
        Delegate = loadDelegate;
        CancellationToken = cancellationToken;
        Description = description;
        CancellationToken?.Register(() => { State = TaskState.Canceled; });
    }

    public event StateChangedHandler<double>? ProgressChanged;
    public event StateChangedHandler<TaskState>? StateChanged;

    event StateChangedHandler<double>? IStateChangedSource<double>.StateChanged
    {
        add => ProgressChanged += value;
        remove => ProgressChanged -= value;
    }
    event StateChangedHandler<TaskState>? IStateChangedSource<TaskState>.StateChanged
    {
        add => StateChanged += value;
        remove => StateChanged -= value;
    }

    /// <summary>
    /// 任务处理进度
    /// </summary>
    public double Progress
    {
        get;
        set
        {
            ProgressChanged?.Invoke(this, field, value);
            field = value;
        }
    } = 0;

    /// <summary>
    /// 任务状态
    /// </summary>
    public TaskState State
    {
        get;
        set
        {
            StateChanged?.Invoke(this, field, value);
            field = value;
        }
    } = TaskState.Waiting;
    
    public object? Result { get; protected set; }

    public string Name { get; protected set; }
    public string? Description { get; protected set; }

    protected CancellationToken? CancellationToken;

    protected Delegate Delegate;

    public virtual object? Run(params object[] objects)
    {
        if (State != TaskState.Waiting)
            throw new InvalidOperationException($"[TaskBase - {Name}] 运行失败：任务已执行");
        State = TaskState.Running;
        try
        {
            var paramTypes = Delegate.GetMethodInfo().GetParameters();
            if (paramTypes.Length == 0 || !paramTypes.First().ParameterType.IsAssignableTo(typeof(TaskBase)))
            {
                if (Delegate.Method.ReturnType.IsAssignableTo(typeof(Task<>)))
                {
                    var task = Convert.ChangeType(Delegate.DynamicInvoke(objects), typeof(Task<>));
                    typeof(Task<>).InvokeMember("RunSynchronously", BindingFlags.InvokeMethod, null, task, []);
                    if (task is not null) 
                        Result = typeof(Task<>).InvokeMember("Result", BindingFlags.GetProperty, null, task, null);
                }
                else if (Delegate.Method.ReturnType.IsAssignableTo(typeof(Task)))
                {
                    var task = (Task?) Delegate.DynamicInvoke(objects);
                    task?.RunSynchronously();
                    Result = null;
                }
                else
                    Result = Delegate.DynamicInvoke(objects);
            }
            else
            {
                if (Delegate.Method.ReturnType.IsAssignableTo(typeof(Task<>)))
                {
                    var task = Convert.ChangeType(Delegate.DynamicInvoke([this, ..objects]), typeof(Task<>));
                    typeof(Task<>).InvokeMember("RunSynchronously", BindingFlags.InvokeMethod, null, task, []);
                    if (task is not null) 
                        Result = typeof(Task<>).InvokeMember("Result", BindingFlags.GetProperty, null, task, null);
                }
                else if (Delegate.Method.ReturnType.IsAssignableTo(typeof(Task)))
                {
                    var task = (Task?) Delegate.DynamicInvoke([this, ..objects]);
                    task?.RunSynchronously();
                    Result = null;
                }
                else
                    Result = Delegate.DynamicInvoke([this, ..objects]);
            }
            
            CancellationToken?.ThrowIfCancellationRequested();
            Progress = 1;
            State = TaskState.Completed;
            
            return Result;
        }
        catch (Exception)
        {
            if (!(CancellationToken?.IsCancellationRequested ?? false))
                State = TaskState.Failed;
            throw;
        }
    }

    public virtual async Task<object?> RunAsync(params object[] objects)
    {
        if (CancellationToken != null)
            return Result = await Task.Run(() => Run(objects), cancellationToken: (CancellationToken)CancellationToken);
        return Result = await Task.Run(() => Run(objects));
    }

    public virtual void RunBackground(params object[] objects)
        => RunAsync(objects).Start();

    public void RegisterCancellationToken(CancellationToken? cancellationToken)
        => CancellationToken = cancellationToken;

    public Type ResultType { get => Delegate.Method.ReturnType; }
    public void Dispose()
    {
        var invocationList = Delegate.GetInvocationList();
        foreach (var handler in invocationList)
        {
            var processedDelegate = Delegate.Remove(Delegate, handler);
            if (processedDelegate is not null)
                Delegate = processedDelegate;
        }
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// 任务原型。<br/>
/// 若需要获取或修改任务信息，传入的委托第一个参数必须为 <see cref="TaskBase"/> 或 <see cref="TaskBase{TResult}"/>。
/// </summary>
/// <typeparam name="TResult">返回类型</typeparam>
public class TaskBase<TResult> : TaskBase
{
    public TaskBase() : base() { }
    protected TaskBase(string name, CancellationToken? cancellationToken = null, string? description = null) : base(name, cancellationToken, description) { }
    public TaskBase(string name, Delegate loadDelegate, CancellationToken? cancellationToken = null, string? description = null) : base(name, loadDelegate, cancellationToken, description) { }

    public new TResult? Result { get; protected set; }

    public new virtual TResult Run(params object[] objects)
    {
        if (State != TaskState.Waiting)
            throw new InvalidOperationException($"[TaskBase - {Name}] 运行失败：任务已执行");
        State = TaskState.Running;
        try
        {
            var paramTypes = Delegate.GetMethodInfo().GetParameters();
            if (paramTypes.Length == 0 ||
                (!paramTypes.First().ParameterType.IsAssignableTo(typeof(TaskBase<TResult>)) &&
                 paramTypes.First().ParameterType != typeof(TaskBase)))
            {
                if (Delegate.Method.ReturnType.IsAssignableTo(typeof(Task<TResult>)))
                {
                    var task = (Task<TResult>?) Delegate.DynamicInvoke(objects);
                    task?.RunSynchronously();
                    if (task is not null)
                        Result = task.Result;
                } 
                else
                    Result = (TResult?) Delegate.DynamicInvoke(objects);
            }
            else
            {
                if (Delegate.Method.ReturnType.IsAssignableTo(typeof(Task<TResult>)))
                {
                    var task = (Task<TResult>?) Delegate.DynamicInvoke([this, ..objects]);
                    task?.RunSynchronously();
                    if (task is not null)
                        Result = task.Result;
                } 
                else
                    Result = (TResult?) Delegate.DynamicInvoke([this, ..objects]);
            }
            
            CancellationToken?.ThrowIfCancellationRequested();
            Progress = 1;
            State = TaskState.Completed;
            
            // Expected warning
            return Result;
        }
        catch (Exception)
        {
            if (!(CancellationToken?.IsCancellationRequested ?? false))
                State = TaskState.Failed;
            throw;
        }
    }

    public new virtual async Task<TResult> RunAsync(params object[] objects)
    {
        if (CancellationToken != null)
            return Result = await Task.Run(() => Run(objects), cancellationToken: (CancellationToken)CancellationToken);
        return Result = await Task.Run(() => Run(objects));
    }

    public override void RunBackground(params object[] objects)
        => RunAsync(objects).Start();

    public new Type ResultType => typeof(TResult);
}