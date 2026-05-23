using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks.Models;

public class SequentialGroupTask : ITask, ITaskGroup, ITaskProgressive
{
    private readonly List<ITask> _tasks = [];
    private double _progress;

    /// <inheritdoc />
    public string Title { get; }

    /// <inheritdoc />
    public TaskState State { get; set; }

    public SequentialGroupTask(string title, IEnumerable<ITask> tasks)
    {

        Title = title;
        _tasks.AddRange(tasks);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(CancellationToken cancelToken = default)
    {
        _UpdateState(TaskState.Running, "开始执行……");
        _progress = 0;

        for (int i = 0; i < _tasks.Count; i++)
        {
            cancelToken.ThrowIfCancellationRequested();

            var task = _tasks[i];
            AddTask?.Invoke(task);

            if (task is ITaskProgressive progressive)
            {
                progressive.ProgressChanged += p =>
                {
                    var weighted = (i + p) / _tasks.Count;
                    ProgressChanged?.Invoke(weighted);
                };
            }

            await task.ExecuteAsync(cancelToken).ConfigureAwait(true);

            _progress = (double)(i + 1) / _tasks.Count;
            ProgressChanged?.Invoke(_progress);
            RemoveTask?.Invoke(task);
        }

        _UpdateState(TaskState.Success, "全部完成");
    }

    private void _UpdateState(TaskState newState, string msg)
    {
        State = newState;
        StateChanged?.Invoke(newState, msg);
    }

    /// <inheritdoc />
    public event TaskStateEvent? StateChanged;

    /// <inheritdoc />
    public event TaskGroupEvent? AddTask;

    /// <inheritdoc />
    public event TaskGroupEvent? RemoveTask;

    /// <inheritdoc />
    public event TaskProgressEvent? ProgressChanged;
}