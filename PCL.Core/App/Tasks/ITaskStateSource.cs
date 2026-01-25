using System.Collections.Generic;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务状态模型。<br/>
/// 实现 <see cref="IStateChangedSource{TProperty}"/> 或直接实现
/// <see cref="IObservableTaskStateSource"/> 以动态响应状态改变。
/// </summary>
public interface ITaskStateSource
{
    /// <summary>
    /// 任务名称。
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// 任务描述。
    /// </summary>
    string? Description { get; }
    
    /// <summary>
    /// 当前任务状态。
    /// </summary>
    TaskState State { get; }
}

/// <summary>
/// 可动态响应状态改变的任务状态模型。
/// </summary>
public interface IObservableTaskStateSource : ITaskStateSource, IStateChangedSource<TaskState>;

/// <summary>
/// 任务状态组模型。
/// </summary>
public interface ITaskStateSourceGroup : ITaskStateSource
{
    IEnumerable<ITaskStateSource> Sources { get; }
}

/// <summary>
/// 可动态响应状态改变的任务状态组模型。
/// </summary>
public interface IObservableTaskStateSourceGroup : ITaskStateSourceGroup, IObservableTaskStateSource;
