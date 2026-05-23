using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.App.Tasks;

public static class TaskEngine
{
    public static async Task ExecuteSequentialAsync(
        IEnumerable<ITask> tasks,
        Action<ITask, TaskState>? onStateChange = null,
        CancellationToken ct = default)
    {
        foreach (var task in tasks)
        {
            ct.ThrowIfCancellationRequested();
            onStateChange?.Invoke(task, TaskState.Running);
            await task.ExecuteAsync(ct).ConfigureAwait(false);
            onStateChange?.Invoke(task, TaskState.Success);
        }
    }

    public static Task ExecuteParallelAsync(
        IEnumerable<ITask> tasks,
        int maxConcurrency = -1,
        CancellationToken ct = default)
    {
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = ct,
            MaxDegreeOfParallelism = maxConcurrency > 0 ? maxConcurrency : 1
        };

        return Parallel.ForEachAsync(tasks, parallelOptions,
            async (task, token) =>
        {
            await task.ExecuteAsync(token).ConfigureAwait(true);
        });
    }
}