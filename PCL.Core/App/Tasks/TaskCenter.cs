using PCL.Core.App.Tasks.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务中心，用于管理任务
/// </summary>
public static class TaskCenter
{
    /// <summary>
    /// Current tasks in the TaskCenter
    /// </summary>
    public static ObservableCollection<TaskModel> CurrentTasks { get; } = [];

    private static readonly ConcurrentDictionary<Guid, TaskModel> _RunningTasks = [];
    private static readonly object _Lock = new();

    static TaskCenter()
    {
        BindingOperations.EnableCollectionSynchronization(CurrentTasks, _Lock);
    }

    /// <summary>
    /// Add a task to <see cref="TaskCenter"/>
    /// </summary>
    /// <param name="task"></param>
    /// <param name="showInList">Is visible in TaskCenter in UI</param>
    /// <returns></returns>
    public static TaskModel Add(ITask task, bool showInList = true)
    {
        var model = _CreateTaskModel(task);

        _RunningTasks.TryAdd(model.Id, model);

        if (showInList)
        {
            lock (_Lock)
            {
                CurrentTasks.Add(model);
            }
        }

        return model;
    }

    /// <summary>
    /// 移除指定的 <see cref="TaskModel"/>
    /// </summary>
    public static void Remove(TaskModel task)
    {
        if (_RunningTasks.TryRemove(task.Id, out var model))
        {
            lock (_Lock)
            {
                CurrentTasks.Remove(model);
                model.Dispose();
            }
        }
    }

    #region Helper Methods

    private static TaskModel _CreateTaskModel(ITask task)
    {
        List<TaskStepModel>? steps = null;

        if (task is IStepTask sTask)
        {
            // TODO: impl dynamic step collection change tracking
            //sTask.Steps.CollectionChanged += (s, e) =>
            //{
            //    _RunInUi(() =>
            //    {
            //        if (e.NewItems is not null)
            //        {
            //            foreach (ITask newItem in e.NewItems)
            //            {
            //                var stepModel = _CreateTaskStepModel(newItem);
            //                steps?.Add(stepModel);
            //            }
            //        }

            //        if (e.OldItems is not null)
            //        {
            //            foreach (ITask oldItem in e.OldItems)
            //            {
            //                var stepModel = steps?.FirstOrDefault(sm => sm.Message == oldItem.Title);
            //                if (stepModel is not null)
            //                {
            //                    steps?.Remove(stepModel);
            //                    stepModel.Dispose();
            //                }
            //            }
            //        }
            //    });
            //};
            steps = sTask.Steps.Select(_CreateTaskStepModel).ToList();
        }

        var pTask = task as IProgressiveTask;

        var model = new TaskModel
        {
            Id = Guid.NewGuid(),
            Title = task.Title,
            State = TaskState.Waiting,
            Steps = steps is not null ? [.. steps] : null,
            SupportProgress = pTask is not null
        };
        task.StateChanged += TaskOnStateChanged;
        pTask?.ProgressChanged += OnPTaskOnProgressChanged;
        model.RegisterCleanup(() =>
        {
            task.StateChanged -= TaskOnStateChanged;
            pTask?.ProgressChanged -= OnPTaskOnProgressChanged;
        });

        return model;

        void OnPTaskOnProgressChanged(double progress) => _RunInUi(() => model.Progress = progress);

        void TaskOnStateChanged(TaskState state, string message)
        {
            _RunInUi(() =>
            {
                model.StateMessage = message;
                model.State = state;
            });

            if (_IsTaskStopped(state))
            {
                model.Dispose();
            }
        }
    }

    private static bool _IsTaskStopped(TaskState state) =>
        state is TaskState.Success or TaskState.Canceled or TaskState.Failed;

    private static TaskStepModel _CreateTaskStepModel(ITask step)
    {
        var pTask = step as IProgressiveTask;
        var supprotProgress = pTask is not null;

        var model = new TaskStepModel
        {
            Message = step.Title,
            Progress = 0.0,
            SupportProgress = supprotProgress
        };

        pTask?.ProgressChanged += OnPTaskOnProgressChanged;
        model.RegisterCleanup(() => { pTask?.ProgressChanged -= OnPTaskOnProgressChanged; });

        return model;

        void OnPTaskOnProgressChanged(double progress) => _RunInUi(() => model.Progress = progress);
    }

    private static void _RunInUi(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.InvokeAsync(action);
        }
    }

    #endregion
}
