using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务组原型。
/// </summary>
public abstract class TaskGroup : TaskBase, IList<TaskBase>
{
    public TaskGroup(string name, IList<TaskBase> tasks, CancellationToken? cancellationToken = null, string? description = null) : base(name, cancellationToken, description)
    {
        Name = name;
        foreach (TaskBase task in tasks)
            task.RegisterCancellationToken(cancellationToken);
        Tasks = new List<TaskBase>(tasks);
        CancellationToken?.Register(() => { State = TaskState.Canceled; });
    }
    public TaskGroup(string name, IList<Delegate> delegates, CancellationToken? cancellationToken = null, string? description = null) : base(name, cancellationToken, description)
    {
        Name = name;
        List<TaskBase> list = [];
        var i = 0;
        foreach (Delegate @delegate in delegates)
        {
            Type returnType = @delegate.Method.ReturnType;
            
            if (returnType == typeof(void))
                list.Add(new TaskBase($"{name} - {i}", @delegate, cancellationToken));
            else
            {
                Type taskBase = typeof(TaskBase<>).MakeGenericType(returnType);
                list.Add((TaskBase)(Activator.CreateInstance(taskBase, $"{name} - {i}", @delegate, cancellationToken) ?? new TaskBase($"{name} - {i}", @delegate, cancellationToken)));
            }
            i++;
        }
        foreach (TaskBase task in list)
            task.RegisterCancellationToken(cancellationToken);
        Tasks = list;
        CancellationToken?.Register(() => { State = TaskState.Canceled; });
    }

    protected List<TaskBase> Tasks;

    TaskBase IList<TaskBase>.this[int index] { get => Tasks[index]; set => Tasks[index] = value; }

    int ICollection<TaskBase>.Count => Tasks.Count;

    bool ICollection<TaskBase>.IsReadOnly => false;

    void ICollection<TaskBase>.Add(TaskBase item)
        => Tasks.Add(item);

    void ICollection<TaskBase>.Clear()
        => Tasks.Clear();

    bool ICollection<TaskBase>.Contains(TaskBase item)
        => Tasks.Contains(item);

    void ICollection<TaskBase>.CopyTo(TaskBase[] array, int arrayIndex)
        => Tasks.CopyTo(array, arrayIndex);

    IEnumerator<TaskBase> IEnumerable<TaskBase>.GetEnumerator()
        => Tasks.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => Tasks.GetEnumerator();

    int IList<TaskBase>.IndexOf(TaskBase item)
        => Tasks.IndexOf(item);

    void IList<TaskBase>.Insert(int index, TaskBase item)
        => Tasks.Insert(index, item);

    bool ICollection<TaskBase>.Remove(TaskBase item)
        => Tasks.Remove(item);

    void IList<TaskBase>.RemoveAt(int index)
        => Tasks.RemoveAt(index);
}

public abstract class TaskGroup<TResult> : TaskBase<TResult>, IList<TaskBase>
{
    public TaskGroup(string name, IList<TaskBase> tasks, CancellationToken? cancellationToken = null, string? description = null) : base(name, cancellationToken, description)
    {
        Name = name;
        foreach (TaskBase task in tasks)
            task.RegisterCancellationToken(cancellationToken);
        Tasks = new List<TaskBase>(tasks);
    }
    public TaskGroup(string name, IList<Delegate> delegates, CancellationToken? cancellationToken = null, string? description = null) : base(name, cancellationToken, description)
    {
        Name = name;
        List<TaskBase> list = [];
        int i = 0;
        foreach (Delegate @delegate in delegates)
        {
            Type returnType = @delegate.Method.ReturnType;
            
            if (returnType == typeof(void))
                list.Add(new TaskBase($"{name} - {i}", @delegate, cancellationToken));
            else
            {
                Type taskBase = typeof(TaskBase<>).MakeGenericType(returnType);
                list.Add((TaskBase)(Activator.CreateInstance(taskBase, $"{name} - {i}", @delegate, cancellationToken) ?? new TaskBase($"{name} - {i}", @delegate, cancellationToken)));
            }
            i++;
        }
        foreach (TaskBase task in list)
            task.RegisterCancellationToken(cancellationToken);
        Tasks = list;
    }

    protected List<TaskBase> Tasks;

    TaskBase IList<TaskBase>.this[int index] { get => Tasks[index]; set => Tasks[index] = value; }

    int ICollection<TaskBase>.Count => Tasks.Count;

    bool ICollection<TaskBase>.IsReadOnly => false;

    void ICollection<TaskBase>.Add(TaskBase item)
        => Tasks.Add(item);

    void ICollection<TaskBase>.Clear()
        => Tasks.Clear();

    bool ICollection<TaskBase>.Contains(TaskBase item)
        => Tasks.Contains(item);

    void ICollection<TaskBase>.CopyTo(TaskBase[] array, int arrayIndex)
        => Tasks.CopyTo(array, arrayIndex);

    IEnumerator<TaskBase> IEnumerable<TaskBase>.GetEnumerator()
        => Tasks.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => Tasks.GetEnumerator();

    int IList<TaskBase>.IndexOf(TaskBase item)
        => Tasks.IndexOf(item);

    void IList<TaskBase>.Insert(int index, TaskBase item)
        => Tasks.Insert(index, item);

    bool ICollection<TaskBase>.Remove(TaskBase item)
        => Tasks.Remove(item);

    void IList<TaskBase>.RemoveAt(int index)
        => Tasks.RemoveAt(index);
}