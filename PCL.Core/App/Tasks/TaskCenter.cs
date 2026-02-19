using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PCL.Core.Logging;

namespace PCL.Core.App.Tasks;

/// <summary>
/// 任务中心，用于管理任务
/// </summary>
public static class TaskCenter
{
    /// <summary>
    /// 可观察的任务模型集合
    /// </summary>
    public static ObservableCollection<TaskModel> Tasks { get; } = [];

    private static readonly ConditionalWeakTable<ITask, TaskModel> _ModelMap = [];

    private static TaskModel _InitModel(ITask instance)
    {
        // ReSharper disable SuspiciousTypeConversion.Global
        var cancelable = instance as ITaskCancelable;
        var pausable = instance as ITaskPausable;
        var progressive = instance as ITaskProgressive;
        // ReSharper restore SuspiciousTypeConversion.Global

        var model = new TaskModel
        {
            Title = instance.Title,
            SupportProgress = progressive != null,
            OnCancel = cancelable == null ? null : (() => cancelable.Cancel()),
            OnPause = pausable == null ? null : (() => pausable.Pause()),
        };

        // state event
        instance.StateChanged += (state, message) =>
        {
            LogWrapper.Trace("TaskCenter", $"{instance.Title}: state changed ({state}): {message}");
            model.State = state;
            model.StateMessage = message;
        };

        // progress event
        if (progressive != null)
        {
            progressive.ProgressChanged += progress =>
            {
                model.Progress = Math.Clamp(progress, 0.0, 1.0);
            };
        }

        // group events
        if (instance is ITaskGroup group)
        {
            group.AddTask += task =>
            {
                var taskModel = _InitModel(task);
                _ModelMap.Add(task, taskModel);
                model.Children.Add(taskModel);
            };
            group.RemoveTask += task =>
            {
                if (_ModelMap.TryGetValue(task, out var taskModel)) model.Children.Remove(taskModel);
            };
        }

        return model;
    }

    /// <summary>
    /// 注册响应式任务实例
    /// </summary>
    /// <param name="instance">任务实例</param>
    /// <param name="start">是否立即启动该实例</param>
    public static void Register(ITask instance, bool start = true)
    {
        var model = _InitModel(instance);
        Tasks.Add(model);

        if (start)
        {
            _ = Task.Run(async () =>
            {
                try { await instance.ExecuteAsync(); }
                catch (OperationCanceledException) { /* ignoring */ }
                catch (Exception ex)
                {
                    LogWrapper.Warn(ex, "TaskCenter", $"{instance.Title}: exception thrown");
                    model.State = TaskState.Failed;
                    model.StateMessage = ex.Message;
                }
            });
        }
    }

    /// <summary>
    /// 移除所有已结束的任务
    /// </summary>
    public static void RemoveFinished()
    {
        foreach (var model in Tasks.Where(x => x.State > TaskState.Running).ToList())
            Tasks.Remove(model);
    }
}
