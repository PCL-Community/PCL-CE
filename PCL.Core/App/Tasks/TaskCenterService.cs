using PCL.Core.App.Tasks.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务中心，用于管理任务
/// </summary>
public static class TaskCenterService
{
    /// <summary>
    /// Current tasks in the TaskCenter
    /// </summary>
    public static ObservableCollection<TaskModel> CurrentTasks { get; } = [];

    private static readonly SemaphoreSlim _ExcuteLock = new(1, 1);


    /// <summary>
    /// Add a task to <see cref="TaskCenterService"/>
    /// </summary>
    /// <param name="task"></param>
    /// <param name="token"></param>
    /// <param name="showInList">Is visible in TaskCenter in UI</param>
    /// <returns></returns>
    public static TaskModel Add(ITask task, CancellationToken token = default, bool showInList = true)
    {
        var model = new TaskModel
        {
            Title = task.Title,
            SupportProgress = task is IProgressiveTask,
            State = TaskState.Waiting,
            StateMessage = "等待执行……"
        };

        if (showInList)
        {
            Application.Current.Dispatcher.InvokeAsync(() => CurrentTasks.Add(model));
        }

        _ = _ProgressTaskAsync(task, model, token);

        return model;
    }

    #region Helper Methods

    /// <summary>
    /// Create a model recuresively
    /// </summary>
    /// <returns>Created model</returns>
    private static TaskModel _CreateTaskModel(ITask task)
    {
        var model = new TaskModel
        {
            Title = task.Title,
            SupportProgress = task is IProgressiveTask,
            State = TaskState.Waiting,
            StateMessage = "等待执行……"
        };

        task.StateChanged += (state, msg) =>
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                model.State = state;
                model.StateMessage = msg;
            });
        };

        if (task is IProgressiveTask pTask)
        {
            pTask.ProgressChanged += (progress) =>
            {
                Application.Current.Dispatcher.InvokeAsync(() => model.Progress = progress);
            };
        }

        if (task is IStepTask stepTask)
        {
            foreach (var stepModel in stepTask.Steps.Select(_CreateTaskModel))
            {
                model.Steps.Add(stepModel);
            }
        }

        return model;
    }

    private static async Task _ProgressTaskAsync(ITask task, TaskModel rootModel, CancellationToken token)
    {
        try
        {
            await _ExcuteLock.WaitAsync(token).ConfigureAwait(false);

            if (rootModel.State is TaskState.Waiting)
            {
                ChangeState(TaskState.Running, "开始执行");
            }

            await task.ExecuteAsync(token).ConfigureAwait(false);

            // for some tasks that do not report state change
            if (rootModel.State is TaskState.Running)
            {
                ChangeState(TaskState.Success, "任务完成");
            }
        }
        catch (OperationCanceledException)
        {
            ChangeState(TaskState.Canceled, "任务已取消");
        }
        catch (Exception ex)
        {
            ChangeState(TaskState.Failed, $"发生错误：{ex.Message}");
        }
        finally
        {
            task.StateChanged -= ChangeState;
            if (task is IProgressiveTask proTask)
            {
                proTask.ProgressChanged -= ChangeProgress;
            }

            _ExcuteLock.Release();
        }

        return;

        void ChangeState(TaskState state, string msg) => _UpdateModel(rootModel, m =>
        {
            m.State = state;
            m.StateMessage = msg;
        });

        void ChangeProgress(double progress) => _UpdateModel(rootModel, m => { m.Progress = progress; });
    }


    private static void _UpdateModel(TaskModel model, Action<TaskModel> action)
    {
        Application.Current.Dispatcher.InvokeAsync(() => action(model));
    }

    #endregion
}
